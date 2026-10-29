using System.Diagnostics;
using System.Text.Json;

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
    // Preserve fields used by the Windows edition when editing a shared library.
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class Library
{
    public int Version { get; set; } = 1;
    public List<Game> Games { get; set; } = new();
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class Store
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Root { get; }
    public string FilePath => Path.Combine(Root, "library.json");
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
    public static ProcessStartInfo Prepare(Game game, bool configure = false)
    {
        string path = Path.GetFullPath(configure ? game.ConfigurationExecutable : game.Executable);
        if (!File.Exists(path)) throw new FileNotFoundException("Executável não encontrado.", path);
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".exe" or ".lnk" or ".bat" or ".cmd" or ".desktop")
            throw new InvalidDataException("Selecione um executável Linux nativo. Pacotes Windows não são executados automaticamente.");
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new InvalidDataException("O arquivo não tem permissão de execução. Confira a origem e use chmod +x se confiar nele.");
        string cwd = configure ? Path.GetDirectoryName(path)! : string.IsNullOrWhiteSpace(game.WorkingDirectory) ? Path.GetDirectoryName(path)! : Path.GetFullPath(game.WorkingDirectory);
        if (!Directory.Exists(cwd)) throw new DirectoryNotFoundException("Pasta de trabalho não encontrada.");
        return new ProcessStartInfo(path, configure ? game.ConfigurationArguments : game.Arguments) { WorkingDirectory = cwd, UseShellExecute = false };
    }
}
