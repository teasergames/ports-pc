using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexus.Native;

public sealed class Game
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string ConfigurationExecutable { get; set; } = "";
    public string ConfigurationArguments { get; set; } = "";
    public string Platform { get; set; } = "Outras";
    public bool Favorite { get; set; }
    public string? LastPlayed { get; set; }
    public string? Artwork { get; set; }
    public string? Cover { get; set; }
    public string? CoverSource { get; set; }
    public string? ArtworkSearchTitle { get; set; }
    // Set for ports installed from the catalog; the folder holds the install receipt.
    public string? Repository { get; set; }
    public string? CatalogId { get; set; }
    public string? InstalledVersion { get; set; }
    public string? ManagedInstallationRoot { get; set; }
    // Preserve fields used by the Windows edition when editing a shared library.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class Library
{
    public int Version { get; set; } = 1;
    public List<Game> Games { get; set; } = new();
    public bool OnlineArtwork { get; set; } = true;
    public bool AutoUpdateLauncher { get; set; } = true;
    public string SortMode { get; set; } = "favorites";
    public string? InstallFolder { get; set; }
    public List<string> SeenCatalogIds { get; set; } = new();
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class Store
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Root { get; }
    public string FilePath => Path.Combine(Root, "library.json");
    public string Covers => Path.Combine(Root, "covers");
    public Store(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }
    public static string DefaultRoot => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg && Path.IsPathRooted(xdg)
            ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
        "NexusPortsPC");
    public static Library Parse(string json)
    {
        var lib = JsonSerializer.Deserialize<Library>(json, Options) ?? throw new InvalidDataException("Biblioteca vazia.");
        if (lib.Version != 1 || lib.Games is null || lib.Games.Any(g => g is null || string.IsNullOrWhiteSpace(g.Id) || string.IsNullOrWhiteSpace(g.Name) || string.IsNullOrWhiteSpace(g.Executable)) || lib.Games.Select(g => g.Id).Distinct().Count() != lib.Games.Count)
            throw new InvalidDataException("Formato de biblioteca inválido.");
        lib.SeenCatalogIds ??= new();
        return lib;
    }
    public Library Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Parse(File.ReadAllText(FilePath)); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            if (!File.Exists(FilePath + ".bak")) throw;
            var backup = Parse(File.ReadAllText(FilePath + ".bak"));
            File.Copy(FilePath, FilePath + ".corrupt-" + Guid.NewGuid().ToString("N"));
            // Restore only after the backup has been validated; never back up corrupt data.
            File.Copy(FilePath + ".bak", FilePath, true);
            return backup;
        }
    }
    public void Save(Library library)
    {
        string json = JsonSerializer.Serialize(library, Options);
        Parse(json);
        string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public static class Launch
{
    // Windows programs run through umu-run (Proton) or Wine when one of them is installed.
    public static string? WindowsRunner()
    {
        string? forced = Environment.GetEnvironmentVariable("NEXUS_WINE");
        if (!string.IsNullOrWhiteSpace(forced)) return File.Exists(forced) ? Path.GetFullPath(forced) : null;
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (string name in new[] { "umu-run", "wine" })
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        return null;
    }

    public static ProcessStartInfo Prepare(Game game, bool configure = false)
    {
        string configured = configure ? game.ConfigurationExecutable : game.Executable;
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidDataException(configure ? "Este jogo não tem executável de configuração." : "Informe o executável.");
        string path = Path.GetFullPath(configured);
        if (!File.Exists(path)) throw new FileNotFoundException("Executável não encontrado.", path);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string cwd = configure ? Path.GetDirectoryName(path)! : string.IsNullOrWhiteSpace(game.WorkingDirectory) ? Path.GetDirectoryName(path)! : Path.GetFullPath(game.WorkingDirectory);
        if (!Directory.Exists(cwd)) throw new DirectoryNotFoundException("Pasta de trabalho não encontrada.");
        string arguments = configure ? game.ConfigurationArguments : game.Arguments;
        if (ext is ".lnk" or ".bat" or ".cmd" or ".desktop")
            throw new InvalidDataException("Atalhos e scripts do Windows não são executados. Selecione o programa.");
        if (ext == ".exe")
        {
            if (OperatingSystem.IsWindows()) return new ProcessStartInfo(path, arguments) { WorkingDirectory = cwd, UseShellExecute = false };
            string runner = WindowsRunner() ?? throw new InvalidDataException("Este é um programa Windows. Instale o Wine ou o umu-launcher (Proton) para abri-lo pelo NEXUS.");
            var info = new ProcessStartInfo(runner) { WorkingDirectory = cwd, UseShellExecute = false };
            info.ArgumentList.Add(path);
            foreach (string arg in SplitArguments(arguments)) info.ArgumentList.Add(arg);
            return info;
        }
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new InvalidDataException("O arquivo não tem permissão de execução. Confira a origem e use chmod +x se confiar nele.");
        return new ProcessStartInfo(path, arguments) { WorkingDirectory = cwd, UseShellExecute = false };
    }

    // Same rules as ProcessStartInfo.Arguments: spaces separate, double quotes group. No shell.
    public static List<string> SplitArguments(string text)
    {
        var result = new List<string>(); var current = new System.Text.StringBuilder(); bool quoted = false, any = false;
        foreach (char c in text ?? "")
        {
            if (c == '"') { quoted = !quoted; any = true; }
            else if (char.IsWhiteSpace(c) && !quoted) { if (any) { result.Add(current.ToString()); current.Clear(); any = false; } }
            else { current.Append(c); any = true; }
        }
        if (any) result.Add(current.ToString());
        return result;
    }

    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidDataException("Endereço inválido.");
        var info = OperatingSystem.IsWindows() ? new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true } : new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        if (!OperatingSystem.IsWindows()) info.ArgumentList.Add(uri.AbsoluteUri);
        Process.Start(info)?.Dispose();
    }

    public static void OpenFolder(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Pasta não encontrada.");
        var info = OperatingSystem.IsWindows() ? new ProcessStartInfo("explorer.exe") { UseShellExecute = false } : new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        info.ArgumentList.Add(folder);
        Process.Start(info)?.Dispose();
    }
}
