using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nexus.Native;

// Core checks for the Linux edition. Everything runs on Linux (CI); on Windows the Unix-only parts
// (execute bits, ELF detection, symbolic links, Wine runner) are skipped so the rest can be checked locally.
bool linux = OperatingSystem.IsLinux();
string root = Path.Combine(Path.GetTempPath(), "nexus-native-" + Guid.NewGuid().ToString("N"));
int passed = 0, skipped = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL " + message); passed++; }
void Reject(Action action, string message) { try { action(); } catch (Exception) { passed++; return; } throw new Exception("FAIL expected rejection: " + message); }
async Task RejectAsync(Func<Task> action, string message) { try { await action(); } catch (Exception) { passed++; return; } throw new Exception("FAIL expected rejection: " + message); }
void Exec(string file) { if (linux) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
byte[] Elf(string tag) => Encoding.ASCII.GetBytes("\u007fELF" + tag);
JsonElement Release(string tag, params (string Name, string Digest)[] assets) => JsonDocument.Parse(JsonSerializer.Serialize(new
{
    tag_name = tag, html_url = "https://github.com/owner/game/releases/tag/" + tag, draft = false, prerelease = false,
    assets = assets.Select(a => new { name = a.Name, size = 10, digest = a.Digest, browser_download_url = "https://github.com/owner/game/releases/download/" + tag + "/" + a.Name })
})).RootElement;
Directory.CreateDirectory(root);
try
{
    // Library persistence.
    var store = new Store(Path.Combine(root, "data"));
    Check(store.Load().Games.Count == 0, "empty library");
    var game = new Game { Name = "Aventura 日本語 ç", Executable = "/bin/echo", Arguments = "\"two words\" literal", Favorite = true };
    var lib = new Library { Games = new() { game } };
    store.Save(lib);
    Check(store.Load().Games.Single().Name == game.Name, "Unicode survives persistence");
    Check(store.Load().Games.Single().Favorite, "favorites persist");
    game.Name = "updated"; store.Save(lib);
    Check(File.Exists(store.FilePath + ".bak"), "backup created");
    File.WriteAllText(store.FilePath, "broken");
    Check(store.Load().Games.Single().Name == "Aventura 日本語 ç", "valid backup recovered");
    Check(Directory.GetFiles(store.Root, "*.corrupt-*").Length == 1, "corrupt original preserved");
    Reject(() => Store.Parse("{\"Version\":2,\"Games\":[]}"), "unknown library version");
    Reject(() => Store.Parse("{\"Version\":1,\"Games\":[null]}"), "null library entry");
    lib.Games.Add(game); Reject(() => store.Save(lib), "duplicate game ID"); lib.Games.RemoveAt(1);
    var foreign = Store.Parse("{\"Version\":1,\"ProtectedApiKey\":\"opaque\",\"Games\":[{\"Id\":\"a\",\"Name\":\"x\",\"Executable\":\"/bin/echo\",\"WindowsOnly\":\"v1\"}]}");
    store.Save(foreign); var roundtrip = store.Load();
    Check(roundtrip.Extra!["ProtectedApiKey"].GetString() == "opaque", "Windows-only library data preserved");
    Check(roundtrip.Games[0].Extra!["WindowsOnly"].GetString() == "v1", "Windows-only game fields preserved");
    Check(Launch.SplitArguments("a \"b c\" d").SequenceEqual(new[] { "a", "b c", "d" }), "arguments split like ProcessStartInfo");

    // Catalog with Linux package fields.
    using var catalog = new Catalog();
    var linuxEntries = catalog.Entries.Where(e => e.HasLinux).ToList();
    Check(catalog.Entries.Count > 300 && linuxEntries.Count >= 50, "embedded catalog has Linux profiles");
    Check(linuxEntries.All(e => Catalog.LinuxKinds.Contains(e.LinuxPackageKind) && !e.ProjectOnly), "Linux profiles are installable");
    Check(linuxEntries.All(e => catalog.CachedLinux(e) is { } r && r.Digest.Length == 64 && r.DownloadUrl.Contains("/" + e.Repository + "/releases/download/", StringComparison.OrdinalIgnoreCase)), "every Linux profile resolves one audited package from the fallback cache");
    Check(catalog.Entries.Select(e => e.Id).Distinct().Count() == catalog.Entries.Count, "catalog IDs unique");
    string sample = "[{\"Id\":\"game\",\"Name\":\"Game\",\"Repository\":\"owner/game\",\"Platform\":\"Nintendo 64\",\"ProjectType\":\"Recompilação\",\"AssetPattern\":\"^game-windows\\\\.zip$\",\"PlayNames\":[],\"ConfigNames\":[],\"LinuxAssetPattern\":\"^game-.*-linux\\\\.tar\\\\.gz$\",\"LinuxPackageKind\":\"Archive\"}]";
    var parsed = Catalog.Parse(sample).Single();
    Check(parsed.HasLinux && parsed.LinuxPackageKind == "Archive", "sample Linux profile parses");
    Reject(() => Catalog.Parse(sample.Replace("\"Archive\"", "\"Deb\"")), "unknown Linux package kind");
    Reject(() => Catalog.Parse(sample.Replace("\"Archive\"", "\"AppImage\"")), "AppImage kind needs AppImage pattern");
    Reject(() => Catalog.Parse(sample.Replace("linux\\\\.tar\\\\.gz$", "linux\\\\.deb$")), "archive pattern must end in a supported format");
    string digest = "sha256:" + new string('a', 64);
    var asset = Catalog.Select(parsed, Release("v1.2.0", ("game-1.2.0-linux.tar.gz", digest), ("game-windows.zip", digest)), false)!;
    Check(asset.AssetName == "game-1.2.0-linux.tar.gz" && asset.Version == "v1.2.0" && asset.Digest == new string('a', 64), "Select picks the Linux package");
    Check(Catalog.Select(parsed, Release("v1", ("game-windows.zip", digest)), false) is null, "Windows-only release has no Linux package");
    Reject(() => Catalog.Select(parsed, Release("v1", ("game-a-linux.tar.gz", digest), ("game-b-linux.tar.gz", digest)), false), "ambiguous Linux packages");
    Reject(() => Catalog.Select(parsed, Release("v1", ("game-1-linux.tar.gz", "")), false), "package without SHA-256");
    Reject(() => Catalog.ValidateDownloadUrl(parsed, "https://github.com/other/game/releases/download/v1/game-1-linux.tar.gz"), "download from another repository");
    Reject(() => Catalog.ValidateDownloadUrl(parsed, "http://github.com/owner/game/releases/download/v1/x.tar.gz"), "plain HTTP download");

    // Archive extraction safety.
    string Zip(string name, Action<ZipArchive> fill) { string f = Path.Combine(root, name); using (var z = ZipFile.Open(f, ZipArchiveMode.Create)) fill(z); return f; }
    void Entry(ZipArchive z, string name, byte[] data, int mode = 0x1ED) { var e = z.CreateEntry(name); e.ExternalAttributes = (0x8000 | mode) << 16; using var s = e.Open(); s.Write(data); }
    string Tar(string name, Action<TarWriter> fill) { string f = Path.Combine(root, name); using (var fs = File.Create(f)) using (var gz = new GZipStream(fs, CompressionLevel.Fastest)) using (var t = new TarWriter(gz)) fill(t); return f; }
    void TarFile(TarWriter t, string name, byte[] data, UnixFileMode mode) => t.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(data), Mode = mode });
    string Fresh(string name) { string d = Path.Combine(root, name); Directory.CreateDirectory(d); return d; }
    Reject(() => Installer.ExtractZip(Zip("evil1.zip", z => Entry(z, "../escape.txt", new byte[1])), Fresh("x1"), default), "zip path traversal");
    Reject(() => Installer.ExtractZip(Zip("evil2.zip", z => Entry(z, "/abs.txt", new byte[1])), Fresh("x2"), default), "zip absolute path");
    Reject(() => Installer.ExtractTarGz(Tar("evil1.tgz", t => TarFile(t, "a/../../escape", new byte[1], UnixFileMode.UserRead)), Fresh("x3"), default), "tar path traversal");
    Check(!File.Exists(Path.Combine(root, "escape.txt")) && !File.Exists(Path.Combine(root, "escape")), "nothing written outside");
    if (linux)
    {
        Reject(() => Installer.ExtractTarGz(Tar("evil2.tgz", t => t.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "link") { LinkName = "/etc" })), Fresh("x4"), default), "tar absolute symlink");
        Reject(() => Installer.ExtractTarGz(Tar("evil3.tgz", t => t.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "link") { LinkName = "../../outside" })), Fresh("x5"), default), "tar symlink escaping");
        string ok = Fresh("x6");
        Installer.ExtractTarGz(Tar("good.tgz", t =>
        {
            TarFile(t, "Game/game.bin", Elf("game"), (UnixFileMode)0xFED); // setuid + group/other write
            t.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "Game/run") { LinkName = "game.bin" });
        }), ok, default);
        var mode = File.GetUnixFileMode(Path.Combine(ok, "Game/game.bin"));
        Check((mode & (UnixFileMode.SetUser | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0 && (mode & UnixFileMode.UserExecute) != 0, "setuid and shared write bits stripped, execute kept");
        Check(new FileInfo(Path.Combine(ok, "Game/run")).LinkTarget == "game.bin", "internal symlink kept");
    }
    else skipped += 4;

    // Program discovery.
    string tree = Fresh("tree");
    void Put(string rel, byte[] data, bool exec = true) { string f = Path.Combine(tree, rel); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllBytes(f, data); if (exec) Exec(f); }
    Put("Game", Elf("main")); Put("bin/Launcher.sh", Encoding.ASCII.GetBytes("#!/bin/sh\n")); Put("lib/libfoo.so.1", Elf("lib")); Put("lib/helper", Elf("x"));
    Put("crashpad_handler", Elf("c")); Put("readme.txt", Encoding.ASCII.GetBytes("hi")); Put("notes.sh", Encoding.ASCII.GetBytes("#!/bin/sh\n"), false);
    var found = Installer.Executables(tree).Select(f => Path.GetRelativePath(tree, f).Replace('\\', '/')).ToArray();
    if (linux) Check(found.SequenceEqual(new[] { "Game", "bin/Launcher.sh" }), "programs found, libraries/helpers/non-executables skipped: " + string.Join(",", found));
    else Check(found.Contains("Game") && found.Contains("bin/Launcher.sh") && !found.Any(f => f.StartsWith("lib/") || f == "crashpad_handler" || f == "readme.txt"), "programs found, libraries and helpers skipped");
    Check(Path.GetFileName(Installer.Executables(tree, new[] { "launcher.sh" })[0]) == "Launcher.sh", "preferred program comes first");

    // Install through a fake GitHub, then update, then uninstall.
    var entry = new PortEntry { Id = "game", Name = "Game", Repository = "owner/game", LinuxAssetPattern = "^game-.*-linux\\.tar\\.gz$", LinuxPackageKind = "Archive" };
    byte[] Package(string version) { using var ms = new MemoryStream(); using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true)) using (var t = new TarWriter(gz)) { TarFile(t, "game-" + version + "/Game", Elf(version), (UnixFileMode)0x1ED); TarFile(t, "game-" + version + "/data.txt", Encoding.ASCII.GetBytes(version), UnixFileMode.UserRead | UnixFileMode.UserWrite); } return ms.ToArray(); }
    ReleaseAsset Asset(string version, byte[] data) => new() { Version = "v" + version, AssetName = "game-" + version + "-linux.tar.gz", DownloadUrl = "https://github.com/owner/game/releases/download/v" + version + "/game-" + version + "-linux.tar.gz", Digest = Hash(data), Size = data.Length };
    var files = new Dictionary<string, byte[]>();
    using var http = new HttpClient(new FakeGitHub(files));
    string parent = Fresh("ports");
    byte[] v1 = Package("1.0"); var a1 = Asset("1.0", v1); files[a1.DownloadUrl] = v1;
    var (folder, programs) = await Installer.Install(http, entry, a1, parent, null, default);
    Check(folder == Path.Combine(parent, "game") && programs.Length == 1 && Path.GetFileName(programs[0]) == "Game", "archive installed and flattened");
    Check(Installer.ReadReceipt(folder) is { CatalogId: "game", Version: "v1.0" }, "install receipt written");
    File.WriteAllText(Path.Combine(folder, "save.dat"), "progress");
    byte[] v2 = Package("2.0"); var a2 = Asset("2.0", v2); files[a2.DownloadUrl] = v2;
    await Installer.Install(http, entry, a2, parent, null, default);
    Check(File.ReadAllText(Path.Combine(folder, "data.txt")) == "2.0" && File.ReadAllText(Path.Combine(folder, "save.dat")) == "progress", "update replaces files and keeps saves");
    Check(File.ReadAllText(Path.Combine(parent, ".nexus-backup-game", "data.txt")) == "1.0", "replaced files backed up");
    var tampered = Asset("3.0", Package("3.0")); files[tampered.DownloadUrl] = Package("3.0-evil");
    await RejectAsync(() => Installer.Install(http, entry, tampered, parent, null, default), "SHA-256 mismatch");
    Check(Installer.ReadReceipt(folder)!.Version == "v2.0" && !Directory.GetDirectories(parent, "*.partial").Any(), "failed download leaves the install untouched");
    var foreignUrl = Asset("2.0", v2); foreignUrl.DownloadUrl = "https://github.com/evil/game/releases/download/v2.0/game-2.0-linux.tar.gz";
    await RejectAsync(() => Installer.Install(http, entry, foreignUrl, parent, null, default), "download URL outside the project");
    string occupied = Path.Combine(parent, "other"); Directory.CreateDirectory(occupied); File.WriteAllText(Path.Combine(occupied, "mine.txt"), "user file");
    var other = new PortEntry { Id = "other", Repository = "owner/game", LinuxAssetPattern = entry.LinuxAssetPattern, LinuxPackageKind = "Archive" };
    await RejectAsync(() => Installer.Install(http, other, a2, parent, null, default), "folder not created by NEXUS");
    Check(File.ReadAllText(Path.Combine(occupied, "mine.txt")) == "user file", "user folder untouched");

    var appEntry = new PortEntry { Id = "app", Repository = "owner/game", LinuxAssetPattern = "^App-.*\\.AppImage$", LinuxPackageKind = "AppImage" };
    byte[] image = Elf("appimage"); var ai = new ReleaseAsset { Version = "v1", AssetName = "App-1.AppImage", DownloadUrl = "https://github.com/owner/game/releases/download/v1/App-1.AppImage", Digest = Hash(image), Size = image.Length };
    files[ai.DownloadUrl] = image;
    var (appFolder, appPrograms) = await Installer.Install(http, appEntry, ai, parent, null, default);
    Check(appPrograms.Single() == Path.Combine(appFolder, "App-1.AppImage"), "AppImage installed as the program");
    if (linux) Check((File.GetUnixFileMode(appPrograms[0]) & UnixFileMode.UserExecute) != 0, "AppImage made executable"); else skipped++;
    byte[] image2 = Elf("appimage2"); var ai2 = new ReleaseAsset { Version = "v2", AssetName = "App-2.AppImage", DownloadUrl = "https://github.com/owner/game/releases/download/v2/App-2.AppImage", Digest = Hash(image2), Size = image2.Length };
    files[ai2.DownloadUrl] = image2;
    await Installer.Install(http, appEntry, ai2, parent, null, default);
    Check(!File.Exists(Path.Combine(appFolder, "App-1.AppImage")) && File.Exists(Path.Combine(appFolder, "App-2.AppImage")), "AppImage update removes the previous version");

    Reject(() => Installer.Uninstall(new Game { CatalogId = "app", ManagedInstallationRoot = occupied }), "uninstall without receipt");
    Check(Directory.Exists(occupied), "folder without receipt kept");
    Reject(() => Installer.Uninstall(new Game { CatalogId = "app", ManagedInstallationRoot = folder }), "uninstall with another port's receipt");
    Installer.Uninstall(new Game { CatalogId = "game", ManagedInstallationRoot = folder });
    Check(!Directory.Exists(folder) && !Directory.Exists(Path.Combine(parent, ".nexus-backup-game")) && Directory.Exists(appFolder), "uninstall removes only this port");

    // Launcher self-update manifest.
    string ver = "9.9.9", name = SelfUpdate.AssetName(ver);
    Check(name == "Nexus-Ports-PC-v9.9.9-Linux-x86_64.AppImage", "launcher asset name");
    var manifest = SelfUpdate.ParseManifest("{\"tag_name\":\"v9.9.9\",\"linux_asset\":\"" + name + "\",\"linux_size\":10,\"linux_digest\":\"" + digest + "\"}");
    Check(manifest is { Version: "9.9.9" } && manifest.DownloadUrl == "https://github.com/teasergames/ports-pc/releases/download/v9.9.9/" + name, "manifest update parsed");
    Check(SelfUpdate.ParseManifest("{\"tag_name\":\"v9.9.9\",\"linux_asset\":\"other.AppImage\",\"linux_size\":10,\"linux_digest\":\"" + digest + "\"}") is null, "manifest with unexpected asset ignored");
    Check(SelfUpdate.ParseManifest("{\"tag_name\":\"v9.9.9\",\"asset\":\"x.zip\"}") is null, "Windows-only manifest ignored");
    Check(AppInfo.Compare("1.10.0", "1.9.9") > 0 && AppInfo.Compare("1.8.0", "1.8.0") == 0, "version comparison");

    // Launching.
    if (linux)
    {
        string exe = Path.Combine(root, "native game ç"); File.Copy("/bin/echo", exe); Exec(exe);
        var g = new Game { Executable = exe, WorkingDirectory = root, Arguments = "\"two words\" literal" };
        var info = Launch.Prepare(g); info.RedirectStandardOutput = true;
        using (var p = Process.Start(info)!) { string output = p.StandardOutput.ReadToEnd(); p.WaitForExit(); Check(p.ExitCode == 0 && output.Trim() == "two words literal", "native process with Unicode path, arguments and cwd"); }
        string marker = Path.Combine(root, "should-not-exist");
        g.Arguments = "; touch " + marker; info = Launch.Prepare(g); info.RedirectStandardOutput = true;
        using (var p = Process.Start(info)!) { p.StandardOutput.ReadToEnd(); p.WaitForExit(); }
        Check(!File.Exists(marker), "arguments are never shell commands");
        File.SetUnixFileMode(exe, UnixFileMode.UserRead);
        Reject(() => Launch.Prepare(g), "non-executable rejected");
        Check(File.GetUnixFileMode(exe) == UnixFileMode.UserRead, "permissions not changed by the launcher");
        string windows = Path.Combine(root, "Game.exe"); File.WriteAllBytes(windows, new byte[] { 0x4D, 0x5A });
        string fakeWine = Path.Combine(root, "fake-wine"); File.Copy("/bin/echo", fakeWine); Exec(fakeWine);
        Environment.SetEnvironmentVariable("NEXUS_WINE", fakeWine);
        info = Launch.Prepare(new Game { Executable = windows, Arguments = "-w \"a b\"" }); info.RedirectStandardOutput = true;
        using (var p = Process.Start(info)!) { string output = p.StandardOutput.ReadToEnd(); p.WaitForExit(); Check(output.Trim() == windows + " -w a b", "Windows program runs through the configured Wine"); }
        Environment.SetEnvironmentVariable("NEXUS_WINE", Path.Combine(root, "missing-wine"));
        Reject(() => Launch.Prepare(new Game { Executable = windows }), "Windows program without Wine explains what to install");
        Environment.SetEnvironmentVariable("NEXUS_WINE", null);
        string lnk = Path.Combine(root, "Game.desktop"); File.WriteAllText(lnk, "[Desktop Entry]"); Exec(lnk);
        Reject(() => Launch.Prepare(new Game { Executable = lnk }), "desktop entries and shortcuts rejected");
    }
    else skipped += 7;
    Reject(() => Launch.Prepare(new Game { Executable = Path.Combine(root, "absent") }), "missing executable");
    Reject(() => Launch.OpenUrl("file:///etc/passwd"), "only https links open");

    Console.WriteLine($"PASS {passed} native checks" + (skipped > 0 ? $" ({skipped} Linux-only skipped on this system)" : ""));
}
finally { try { Directory.Delete(root, true); } catch (Exception) { } }

sealed class FakeGitHub(Dictionary<string, byte[]> files) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(files.TryGetValue(request.RequestUri!.ToString(), out var data)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
}
