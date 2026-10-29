using Nexus.Native;
using System.Diagnostics;
using System.Text.Json;

if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Execute estes testes em Linux.");

string root = Path.Combine(Path.GetTempPath(), "nexus-native-" + Guid.NewGuid().ToString("N"));
int passed = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
void Reject(Action action, string message) { try { action(); } catch (Exception) { passed++; return; } throw new Exception("Expected rejection: " + message); }
try
{
    var store = new Store(root);
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
    Check(Directory.GetFiles(root, "*.corrupt-*").Length == 1, "corrupt original preserved");
    Check(Store.Parse(File.ReadAllText(store.FilePath)).Games.Count == 1, "primary restored before future saves");
    Reject(() => Store.Parse("{\"Version\":2,\"Games\":[]}"), "unknown version");
    Reject(() => Store.Parse("{\"Version\":1,\"Games\":[null]}"), "null entry");
    lib.Games.Add(game); Reject(() => store.Save(lib), "duplicate ID"); lib.Games.RemoveAt(1);
    var foreign = Store.Parse("{\"Version\":1,\"OnlineArtwork\":false,\"ProtectedApiKey\":\"opaque\",\"Games\":[{\"Id\":\"a\",\"Name\":\"x\",\"Executable\":\"/bin/echo\",\"InstalledVersion\":\"v1\"}]}");
    store.Save(foreign); var roundtrip = store.Load();
    Check(roundtrip.Extra!["ProtectedApiKey"].GetString() == "opaque", "Windows DPAPI data preserved without decryption");
    Check(roundtrip.Games[0].Extra!["InstalledVersion"].GetString() == "v1", "Windows receipt metadata preserved");
    string exe = Path.Combine(root, "native game ç"); File.Copy("/bin/echo", exe);
    File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    game.Executable = exe; game.WorkingDirectory = root;
    var info = Launch.Prepare(game); info.RedirectStandardOutput = true;
    using (var process = Process.Start(info)!) { string output = process.StandardOutput.ReadToEnd(); process.WaitForExit(); Check(process.ExitCode == 0 && output.Trim() == "two words literal", "real native process, Unicode/spaced path, arguments and cwd"); }
    string marker = Path.Combine(root, "should-not-exist");
    game.Arguments = "; touch " + marker; info = Launch.Prepare(game); info.RedirectStandardOutput = true;
    using (var process = Process.Start(info)!) { process.StandardOutput.ReadToEnd(); process.WaitForExit(); }
    Check(!File.Exists(marker), "arguments are never shell commands");
    game.ConfigurationExecutable = "/bin/echo"; game.ConfigurationArguments = "setup";
    Check(Launch.Prepare(game, true).Arguments == "setup", "configuration launcher has separate arguments");
    string windows = Path.Combine(root, "Game.exe"); File.Copy(exe, windows); game.Executable = windows;
    Reject(() => Launch.Prepare(game), "Windows files rejected");
    game.Executable = exe; File.SetUnixFileMode(exe, UnixFileMode.UserRead);
    Reject(() => Launch.Prepare(game), "non-executable rejected without chmod mutation");
    File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserExecute);
    game.WorkingDirectory = Path.Combine(root, "missing"); Reject(() => Launch.Prepare(game), "missing cwd");
    game.Executable = Path.Combine(root, "absent"); Reject(() => Launch.Prepare(game), "missing executable");
    Console.WriteLine($"PASS {passed} native checks");
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
