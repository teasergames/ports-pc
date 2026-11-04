using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexus.Native;

public sealed class LauncherUpdate
{
    public string Version = "", AssetName = "", DownloadUrl = "", Digest = "";
    public long Size;
}

// Updates the AppImage in place: the new file is downloaded beside it, checked against the SHA-256
// published by GitHub, and renamed over the old one only after verification.
public static class SelfUpdate
{
    const string Repository = "teasergames/ports-pc";
    public static string AssetName(string version) => "Nexus-Ports-PC-v" + version + "-Linux-x86_64.AppImage";

    public static async Task<LauncherUpdate?> Check(HttpClient http, CancellationToken token)
    {
        LauncherUpdate? found;
        try
        {
            using var response = await http.GetAsync("https://api.github.com/repos/" + Repository + "/releases/latest", token);
            if ((int)response.StatusCode is 403 or 429) found = await FromManifest(http, token);
            else
            {
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                found = FromRelease(doc.RootElement);
            }
        }
        catch (HttpRequestException) { found = await FromManifest(http, token); }
        return found is not null && AppInfo.Compare(found.Version, AppInfo.Version) > 0 ? found : null;
    }

    public static LauncherUpdate? FromRelease(JsonElement release)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        string version = (release.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")) return null;
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != AssetName(version)) continue;
            string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
            if (!Regex.IsMatch(digest, "^sha256:[0-9a-f]{64}$")) return null;
            return Validated(new LauncherUpdate { Version = version, AssetName = AssetName(version), DownloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "", Digest = digest[7..], Size = asset.GetProperty("size").GetInt64() });
        }
        return null;
    }

    static async Task<LauncherUpdate?> FromManifest(HttpClient http, CancellationToken token)
    {
        try
        {
            string json = await http.GetStringAsync("https://github.com/" + Repository + "/releases/latest/download/nexus-update.json", token);
            return ParseManifest(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException) { return null; }
    }

    public static LauncherUpdate? ParseManifest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("linux_asset", out var asset)) return null;
        string version = (root.GetProperty("tag_name").GetString() ?? "").TrimStart('v');
        string digest = root.GetProperty("linux_digest").GetString() ?? "";
        if (asset.GetString() != AssetName(version) || !Regex.IsMatch(digest, "^sha256:[0-9a-f]{64}$")) return null;
        return Validated(new LauncherUpdate { Version = version, AssetName = AssetName(version), Digest = digest[7..], Size = root.GetProperty("linux_size").GetInt64(), DownloadUrl = "https://github.com/" + Repository + "/releases/download/v" + version + "/" + AssetName(version) });
    }

    static LauncherUpdate Validated(LauncherUpdate update)
    {
        if (!Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || uri.AbsolutePath != "/" + Repository + "/releases/download/v" + update.Version + "/" + update.AssetName)
            throw new InvalidDataException("Endereço de atualização inesperado.");
        return update;
    }

    public static string? RunningAppImage()
    {
        string? path = Environment.GetEnvironmentVariable("APPIMAGE");
        return !string.IsNullOrEmpty(path) && File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    public static async Task Apply(HttpClient http, LauncherUpdate update, string appImage, IProgress<double>? progress, CancellationToken token)
    {
        string temp = appImage + ".download-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var response = await http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(token);
                await using var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write);
                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[1 << 16]; long done = 0; int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token); sha.AppendData(buffer, 0, read); done += read;
                    if (done > update.Size) throw new InvalidDataException("O download excedeu o tamanho publicado.");
                    progress?.Report(update.Size > 0 ? done * 100.0 / update.Size : 0);
                }
                if (!string.Equals(Convert.ToHexStringLower(sha.GetHashAndReset()), update.Digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O SHA-256 da atualização não confere. Nada foi alterado.");
            }
            Installer.MakeExecutable(temp);
            File.Move(temp, appImage, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
