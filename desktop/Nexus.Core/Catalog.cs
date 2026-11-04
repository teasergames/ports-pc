using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexus.Native;

public static class Platforms
{
    public static readonly string[] All = { "PlayStation 4", "PlayStation 3", "PlayStation 2", "PlayStation", "PSP", "Xbox", "Xbox 360", "Xbox One", "Nintendo Switch", "Nintendo 64", "GameCube", "Wii", "Wii U", "Super Nintendo", "Game Boy", "Game Boy Advance", "Nintendo DS", "Dreamcast", "Outras" };
}

public sealed class PortEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Repository { get; set; } = "";
    public string Platform { get; set; } = "Outras";
    public string ArtworkTitle { get; set; } = "";
    public string ProjectType { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool AllowPrerelease { get; set; }
    public bool ProjectOnly { get; set; }
    public bool ManualPackage { get; set; }
    public string? SourceUrl { get; set; }
    public string? MinNexusVersion { get; set; }
    // Linux package of the same project. AppImage files run directly; archives are extracted.
    public string? LinuxAssetPattern { get; set; }
    public string? LinuxPackageKind { get; set; }
    public string[]? LinuxPlayNames { get; set; }
    public string? LinuxNotes { get; set; }
    public bool HasLinux => !string.IsNullOrEmpty(LinuxAssetPattern);
    public string ProjectUrl => string.IsNullOrEmpty(SourceUrl) ? "https://github.com/" + Repository : SourceUrl!;
    public string CanonicalTitle => string.IsNullOrWhiteSpace(ArtworkTitle) ? Catalog.CanonicalTitle(Name) : ArtworkTitle;
}

public sealed class ReleaseAsset
{
    public string Version = "", ReleaseUrl = "", AssetName = "", DownloadUrl = "", Digest = "";
    public long Size;
    public bool IsPrerelease, FromCache;
}

public sealed class Catalog : IDisposable
{
    public const string RemoteCatalogUrl = "https://raw.githubusercontent.com/teasergames/ports-pc/main/assets/ports.json";
    public const string RemoteReleasesCacheUrl = "https://raw.githubusercontent.com/teasergames/ports-pc/catalog-data/releases-cache.json";
    public static readonly string[] LinuxKinds = { "AppImage", "Archive" };
    const int Limit = 8 * 1024 * 1024;
    readonly HttpClient client;
    public List<PortEntry> Entries { get; private set; }
    Dictionary<string, JsonElement> cache = new(StringComparer.OrdinalIgnoreCase);

    public Catalog(HttpMessageHandler? handler = null)
    {
        client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NexusPortsPC-Linux/" + AppInfo.Version + " (https://github.com/teasergames/ports-pc)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        Entries = Parse(Embedded("Nexus.ports.json"));
        LoadCache(Embedded("Nexus.releases-cache.json"));
    }

    static string Embedded(string name)
    {
        using var stream = typeof(Catalog).Assembly.GetManifestResourceStream(name) ?? throw new InvalidDataException("Recurso ausente: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string CanonicalTitle(string name)
    {
        int split = name.IndexOf(" · ", StringComparison.Ordinal);
        return (split > 0 ? name[..split] : name).Trim();
    }

    // Validates the whole document, like the Windows edition: one invalid profile rejects the catalog.
    public static List<PortEntry> Parse(string json)
    {
        var entries = JsonSerializer.Deserialize<List<PortEntry>>(json) ?? throw new InvalidDataException("Catálogo vazio.");
        if (entries.Count == 0) throw new InvalidDataException("Catálogo vazio.");
        var ids = new HashSet<string>();
        foreach (var e in entries)
        {
            if (e is null || !Regex.IsMatch(e.Id ?? "", "^[a-z0-9-]+$") || !ids.Add(e.Id!) || string.IsNullOrWhiteSpace(e.Name))
                throw new InvalidDataException("Entrada do catálogo inválida.");
            if (!e.ManualPackage && !Regex.IsMatch(e.Repository ?? "", "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new InvalidDataException("Repositório inválido: " + e.Id);
            if (!Platforms.All.Contains(e.Platform)) throw new InvalidDataException("Plataforma inválida: " + e.Id);
            if (e.HasLinux)
            {
                if (!LinuxKinds.Contains(e.LinuxPackageKind) || e.ManualPackage) throw new InvalidDataException("Perfil Linux inválido: " + e.Id);
                bool appImage = e.LinuxPackageKind == "AppImage";
                if (appImage ? !e.LinuxAssetPattern!.EndsWith("AppImage$", StringComparison.Ordinal) : !Regex.IsMatch(e.LinuxAssetPattern!, @"(\\\.zip|\\\.tar\\\.gz|\\\.tgz)\$$"))
                    throw new InvalidDataException("Pacote Linux não suportado: " + e.Id);
                _ = new Regex(e.LinuxAssetPattern!);
            }
            e.LinuxPlayNames ??= Array.Empty<string>();
        }
        return entries;
    }

    void LoadCache(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var next = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in doc.RootElement.EnumerateArray())
            if (record.TryGetProperty("Repository", out var repo) && record.TryGetProperty("Release", out var release))
                next[repo.GetString() ?? ""] = release.Clone();
        cache = next;
    }

    // The catalog on the main branch is served by raw.githubusercontent.com, outside the API limit.
    public async Task<bool> RefreshFromRemote(string dataRoot, CancellationToken token)
    {
        string file = Path.Combine(dataRoot, "catalog-cache.json");
        List<PortEntry>? remote = null;
        try
        {
            string json = await FetchText(RemoteCatalogUrl, token);
            remote = Parse(json);
            await File.WriteAllTextAsync(file, json, token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        if (remote is null && File.Exists(file)) { try { remote = Parse(await File.ReadAllTextAsync(file, token)); } catch (Exception ex) when (ex is not OperationCanceledException) { } }
        try { LoadCache(await FetchText(RemoteReleasesCacheUrl, token)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
        if (remote is null) return false;
        Entries = remote; return true;
    }

    public bool Supported(PortEntry e) => e.MinNexusVersion is null || AppInfo.Compare(e.MinNexusVersion, AppInfo.Version) <= 0;

    async Task<string> FetchText(string url, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > Limit) throw new InvalidDataException("Resposta muito grande.");
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        if (bytes.Length > Limit) throw new InvalidDataException("Resposta muito grande.");
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    // Latest Linux package of a profile. Uses the GitHub API and, when it is rate limited or offline,
    // the release recorded in the daily cache (which lists every asset of that release).
    public async Task<ReleaseAsset> LatestLinux(PortEntry entry, CancellationToken token)
    {
        if (!entry.HasLinux) throw new InvalidDataException("Este projeto não publica build Linux integrada.");
        try
        {
            string json = await FetchText("https://api.github.com/repos/" + entry.Repository + "/releases?per_page=30", token);
            using var doc = JsonDocument.Parse(json);
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                var found = Select(entry, release, false);
                if (found is not null) return found;
            }
            throw new InvalidDataException("Nenhuma Release com o pacote Linux esperado.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429 or null)
        {
            if (cache.TryGetValue(entry.Repository, out var cached) && Select(entry, cached, true) is { } fromCache) return fromCache;
            throw new InvalidDataException(ex.StatusCode is null ? "Sem conexão com o GitHub." : "O GitHub limitou temporariamente as consultas desta rede. Tente novamente em alguns minutos.");
        }
    }

    public ReleaseAsset? CachedLinux(PortEntry entry) => entry.HasLinux && cache.TryGetValue(entry.Repository, out var r) ? Select(entry, r, true) : null;

    public static ReleaseAsset? Select(PortEntry entry, JsonElement release, bool fromCache)
    {
        if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        bool pre = release.TryGetProperty("prerelease", out var p) && p.GetBoolean();
        if (pre && !entry.AllowPrerelease) return null;
        if (!release.TryGetProperty("assets", out var assets)) return null;
        var regex = new Regex(entry.LinuxAssetPattern!, RegexOptions.IgnoreCase);
        var matches = assets.EnumerateArray().Where(a => regex.IsMatch(a.GetProperty("name").GetString() ?? "")).ToList();
        if (matches.Count == 0) return null;
        if (matches.Count > 1) throw new InvalidDataException("A Release tem mais de um pacote Linux compatível; o perfil precisa ser revisado.");
        var asset = matches[0];
        string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
        if (!Regex.IsMatch(digest, "^sha256:[0-9a-f]{64}$")) throw new InvalidDataException("A Release não publica o SHA-256 do pacote; a instalação foi bloqueada.");
        string url = asset.GetProperty("browser_download_url").GetString() ?? "";
        ValidateDownloadUrl(entry, url);
        return new ReleaseAsset
        {
            Version = release.GetProperty("tag_name").GetString() ?? "",
            ReleaseUrl = release.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "",
            AssetName = asset.GetProperty("name").GetString() ?? "",
            DownloadUrl = url, Digest = digest[7..],
            Size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0,
            IsPrerelease = pre, FromCache = fromCache
        };
    }

    public static void ValidateDownloadUrl(PortEntry entry, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.AbsolutePath.StartsWith("/" + entry.Repository + "/releases/download/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O pacote não pertence às Releases do projeto selecionado.");
    }

    public HttpClient Http => client;
    public void Dispose() => client.Dispose();
}

public static class AppInfo
{
    public static string Version { get; } = Read();
    static string Read()
    {
        var attr = (Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly).GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        int plus = attr.IndexOf('+');
        return plus > 0 ? attr[..plus] : attr;
    }
    // Numeric comparison of x.y.z versions; a suffix (beta) sorts before the plain version.
    public static int Compare(string a, string b)
    {
        static (int[] parts, bool pre) Split(string v)
        {
            v = v.TrimStart('v', 'V'); int dash = v.IndexOf('-'); bool pre = dash >= 0; if (pre) v = v[..dash];
            var parts = v.Split('.').Select(x => int.TryParse(x, out int n) ? n : 0).ToList(); while (parts.Count < 3) parts.Add(0);
            return (parts.Take(3).ToArray(), pre);
        }
        var (pa, xa) = Split(a); var (pb, xb) = Split(b);
        for (int i = 0; i < 3; i++) if (pa[i] != pb[i]) return pa[i].CompareTo(pb[i]);
        return xa == xb ? 0 : xa ? -1 : 1;
    }
}
