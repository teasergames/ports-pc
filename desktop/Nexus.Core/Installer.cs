using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Nexus.Native;

public sealed class InstallReceipt
{
    public string CatalogId { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Version { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Digest { get; set; } = "";
    public string Executable { get; set; } = "";
}

public sealed class InstallProgress
{
    public string Message = "";
    public double Percent;
}

// Installs Linux packages published in a project's GitHub Releases into <parent>/<catalog id>.
public static class Installer
{
    public const string ReceiptName = ".nexus-receipt.json";
    static readonly string[] Helpers = { "crashpad_handler", "zig", "clang", "clang++", "lld", "ld.lld", "python3", "python", "ffmpeg", "ffprobe", "7z", "hactool", "chrome-sandbox", "AppRun" };
    static readonly string[] SkipDirs = { "lib", "lib64", "lib32", "libs", "python", "venv", ".venv", "llvm", "toolchain", "zig", "tools", "_internal" };

    // Default ports folder: next to the AppImage (or the program) when writable, else in the data folder.
    public static string DefaultFolder(string dataRoot)
    {
        string? appImage = Environment.GetEnvironmentVariable("APPIMAGE");
        string beside = Path.GetDirectoryName(string.IsNullOrEmpty(appImage) ? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) : appImage)!;
        string candidate = Path.Combine(beside, "ports");
        return Writable(beside) && !beside.StartsWith("/tmp/.mount_", StringComparison.Ordinal) ? candidate : Path.Combine(dataRoot, "ports");
    }

    public static bool Writable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, ".nexus-write-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, ""); File.Delete(probe); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public static string TargetFolder(string parent, PortEntry entry) => Path.Combine(Path.GetFullPath(parent), entry.Id);

    public static InstallReceipt? ReadReceipt(string folder)
    {
        string file = Path.Combine(folder, ReceiptName);
        if (!File.Exists(file)) return null;
        try { return JsonSerializer.Deserialize<InstallReceipt>(File.ReadAllText(file)); } catch (JsonException) { return null; }
    }

    // Downloads, verifies the SHA-256 published by GitHub, unpacks, and returns the folder and the
    // executables found. Updates copy the new files over the existing folder so saves and settings
    // kept inside it survive; replaced files are kept in a sibling backup folder.
    public static async Task<(string Folder, string[] Executables)> Install(HttpClient http, PortEntry entry, ReleaseAsset release, string parent,
        IProgress<InstallProgress>? progress, CancellationToken token)
    {
        Catalog.ValidateDownloadUrl(entry, release.DownloadUrl);
        if (release.AssetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || release.AssetName is "." or ".." || release.AssetName.Contains('/'))
            throw new InvalidDataException("Nome de pacote inválido.");
        parent = Path.GetFullPath(parent);
        Directory.CreateDirectory(parent);
        string target = TargetFolder(parent, entry);
        var existing = Directory.Exists(target) ? ReadReceipt(target) : null;
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any() && (existing is null || existing.CatalogId != entry.Id))
            throw new InvalidDataException("A pasta " + target + " já existe e não foi criada pelo NEXUS para este port. Escolha outra pasta.");
        string staging = Path.Combine(parent, "." + entry.Id + "." + Guid.NewGuid().ToString("N") + ".partial");
        Directory.CreateDirectory(staging);
        try
        {
            string download = Path.Combine(staging, "download");
            await Download(http, release, download, progress, token);
            string payload = Path.Combine(staging, "payload");
            Directory.CreateDirectory(payload);
            progress?.Report(new InstallProgress { Message = "Preparando arquivos…", Percent = 90 });
            if (entry.LinuxPackageKind == "AppImage")
            {
                string file = Path.Combine(payload, release.AssetName);
                File.Move(download, file);
                MakeExecutable(file);
            }
            else if (release.AssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ExtractZip(download, payload, token);
            else ExtractTarGz(download, payload, token);
            if (File.Exists(download)) File.Delete(download);
            Flatten(payload);
            Merge(payload, target, Path.Combine(parent, ".nexus-backup-" + entry.Id), token);
            if (existing is not null && entry.LinuxPackageKind == "AppImage" && !string.Equals(existing.Asset, release.AssetName, StringComparison.Ordinal))
            {
                // A versioned AppImage name leaves the previous file behind; remove it only when it is ours.
                string old = Path.Combine(target, existing.Asset);
                if (existing.Asset.EndsWith(".AppImage", StringComparison.Ordinal) && File.Exists(old) && Path.GetDirectoryName(Path.GetFullPath(old)) == target) File.Delete(old);
            }
            var found = entry.LinuxPackageKind == "AppImage" ? new[] { Path.Combine(target, release.AssetName) } : Executables(target, entry.LinuxPlayNames);
            if (found.Length == 0) throw new InvalidDataException("O pacote não contém um programa Linux identificável. Nada foi cadastrado.");
            WriteReceipt(target, new InstallReceipt { CatalogId = entry.Id, Repository = entry.Repository, Version = release.Version, Asset = release.AssetName, Digest = release.Digest, Executable = existing?.Executable ?? "" });
            progress?.Report(new InstallProgress { Message = "Instalado.", Percent = 100 });
            return (target, found);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    public static void WriteReceipt(string folder, InstallReceipt receipt) =>
        File.WriteAllText(Path.Combine(folder, ReceiptName), JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));

    static async Task Download(HttpClient http, ReleaseAsset release, string file, IProgress<InstallProgress>? progress, CancellationToken token)
    {
        using var response = await http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? release.Size;
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, true);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1 << 16]; long done = 0; int read; DateTime last = DateTime.MinValue;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token); sha.AppendData(buffer, 0, read); done += read;
            if (release.Size > 0 && done > release.Size) throw new InvalidDataException("O download excedeu o tamanho publicado.");
            if (DateTime.UtcNow - last > TimeSpan.FromMilliseconds(250))
            {
                last = DateTime.UtcNow;
                progress?.Report(new InstallProgress { Message = "Baixando " + release.AssetName + " · " + (done / 1048576) + " MB" + (total > 0 ? " de " + (total / 1048576) + " MB" : ""), Percent = total > 0 ? done * 88.0 / total : 0 });
            }
        }
        string hash = Convert.ToHexStringLower(sha.GetHashAndReset());
        if (!string.Equals(hash, release.Digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O SHA-256 do download não confere com o publicado no GitHub. Nada foi instalado.");
    }

    static string Inside(string root, string relative)
    {
        relative = relative.Replace('\\', '/');
        if (relative.StartsWith('/') || relative.Split('/').Contains("..")) throw new InvalidDataException("O pacote tenta gravar fora da pasta de instalação: " + relative);
        string full = Path.GetFullPath(Path.Combine(root, relative));
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal) && full != prefix.TrimEnd(Path.DirectorySeparatorChar)) throw new InvalidDataException("O pacote tenta gravar fora da pasta de instalação: " + relative);
        return full;
    }

    static void LinkInside(string root, string link, string target)
    {
        // Symbolic links may only point inside the installation.
        if (Path.IsPathRooted(target)) throw new InvalidDataException("Link simbólico absoluto no pacote: " + target);
        string resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(link)!, target));
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Link simbólico aponta para fora do pacote: " + target);
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        File.CreateSymbolicLink(link, target);
    }

    public static void ExtractZip(string archive, string destination, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var item in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(item.FullName)) continue;
            string path = Inside(destination, item.FullName);
            int mode = (int)((uint)item.ExternalAttributes >> 16);
            if (item.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
            if ((mode & 0xF000) == 0xA000)
            {
                using var reader = new StreamReader(item.Open());
                LinkInside(destination, path, reader.ReadToEnd());
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            item.ExtractToFile(path, false);
            if (!OperatingSystem.IsWindows() && (mode & 0x1FF) != 0) File.SetUnixFileMode(path, (UnixFileMode)(mode & 0x1ED));
        }
    }

    public static void ExtractTarGz(string archive, string destination, CancellationToken token)
    {
        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        TarEntry? item;
        while ((item = tar.GetNextEntry()) is not null)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(item.Name) || item.Name is "./" or ".") continue;
            string path = Inside(destination, item.Name);
            switch (item.EntryType)
            {
                case TarEntryType.Directory: Directory.CreateDirectory(path); break;
                case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    item.ExtractToFile(path, false);
                    // Never keep setuid/setgid/sticky bits or write access for others.
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, item.Mode & (UnixFileMode)0x1ED);
                    break;
                case TarEntryType.SymbolicLink: LinkInside(destination, path, item.LinkName); break;
                case TarEntryType.HardLink:
                    string source = Inside(destination, item.LinkName);
                    if (File.Exists(source)) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.Copy(source, path); }
                    break;
                default: break; // Devices, FIFOs and other special files are ignored.
            }
        }
    }

    // Packages usually wrap everything in one folder; install its contents instead.
    static void Flatten(string payload)
    {
        while (true)
        {
            var items = Directory.GetFileSystemEntries(payload);
            if (items.Length != 1 || !Directory.Exists(items[0]) || new FileInfo(items[0]).LinkTarget is not null) return;
            string inner = items[0], temp = payload + ".flatten";
            Directory.Move(inner, temp);
            Directory.Delete(payload);
            Directory.Move(temp, payload);
        }
    }

    static void Merge(string source, string target, string backup, CancellationToken token)
    {
        bool update = Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any();
        if (!update)
        {
            if (Directory.Exists(target)) Directory.Delete(target);
            Directory.Move(source, target);
            return;
        }
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            string relative = Path.GetRelativePath(source, file);
            string destination = Path.Combine(target, relative);
            if (File.Exists(destination) || new FileInfo(destination).LinkTarget is not null)
            {
                string saved = Path.Combine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                File.Move(destination, saved);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(file, destination);
        }
    }

    public static void MakeExecutable(string file)
    {
        if (OperatingSystem.IsWindows()) return;
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    // Linux programs in an installed folder: ELF binaries, AppImages and shell launchers with the
    // execute bit, skipping bundled libraries and toolchains. Preferred names come first.
    public static string[] Executables(string root, string[]? preferred = null)
    {
        var found = new List<string>();
        void Walk(string dir, int depth)
        {
            if (depth > 4) return;
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith('.') || name.Contains(".so") || Helpers.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                if (new FileInfo(file).LinkTarget is not null) continue;
                if (IsProgram(file)) found.Add(file);
            }
            foreach (string sub in Directory.EnumerateDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (name.StartsWith('.') || SkipDirs.Contains(name, StringComparer.OrdinalIgnoreCase) || new DirectoryInfo(sub).LinkTarget is not null) continue;
                Walk(sub, depth + 1);
            }
        }
        Walk(root, 0);
        var ordered = found.OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar)).ThenBy(f => f, StringComparer.Ordinal).ToList();
        if (preferred is { Length: > 0 })
            foreach (string name in preferred.Reverse())
            {
                var match = ordered.FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
                if (match is not null) { ordered.Remove(match); ordered.Insert(0, match); }
            }
        return ordered.ToArray();
    }

    static bool IsProgram(string file)
    {
        try
        {
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(file) & UnixFileMode.UserExecute) == 0) return false;
            using var stream = File.OpenRead(file);
            Span<byte> head = stackalloc byte[4];
            if (stream.Read(head) < 2) return false;
            if (head[0] == 0x7F && head[1] == (byte)'E' && head[2] == (byte)'L' && head[3] == (byte)'F') return true;
            return head[0] == (byte)'#' && head[1] == (byte)'!' && file.EndsWith(".sh", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    // Deletes a catalog installation. Only folders holding this port's receipt are removed; symbolic
    // links are deleted as links, never followed.
    public static void Uninstall(Game game)
    {
        string? root = game.ManagedInstallationRoot;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) throw new DirectoryNotFoundException("A pasta do port não existe mais.");
        var receipt = ReadReceipt(root) ?? throw new InvalidDataException("Esta pasta não tem o recibo de instalação do NEXUS; nada foi apagado.");
        if (receipt.CatalogId != game.CatalogId) throw new InvalidDataException("O recibo da pasta pertence a outro port; nada foi apagado.");
        if (new DirectoryInfo(root).LinkTarget is not null) throw new InvalidDataException("A pasta do port é um link; nada foi apagado.");
        Directory.Delete(root, true);
        string backup = Path.Combine(Path.GetDirectoryName(root)!, ".nexus-backup-" + receipt.CatalogId);
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
    }
}
