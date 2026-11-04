using System.Text.Json;

namespace Nexus.Native;

// Portrait covers for the library cards: the infobox image of the game's Wikipedia article,
// which is normally the official box art, with a search fallback when no article has that title.
public static class Covers
{
    public static async Task<(string File, string Source)?> Find(HttpClient http, string title, string folder, string id, CancellationToken token)
    {
        if (title == "Shadow of the Colossus") title = "Shadow of the Colossus (2018 video game)";
        string exact = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&redirects=1&prop=pageimages&piprop=original&pilicense=any&titles=" + Uri.EscapeDataString(title);
        var found = await FromPages(http, exact, folder, id, token);
        if (found is not null) return found;
        string search = "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&generator=search&gsrlimit=3&gsrnamespace=0&prop=pageimages&piprop=original&pilicense=any&gsrsearch=" + Uri.EscapeDataString(title + " video game");
        return await FromPages(http, search, folder, id, token);
    }

    static async Task<(string, string)?> FromPages(HttpClient http, string endpoint, string folder, string id, CancellationToken token)
    {
        using var response = await http.GetAsync(endpoint, token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!doc.RootElement.TryGetProperty("query", out var query) || !query.TryGetProperty("pages", out var pages)) return null;
        var ordered = pages.EnumerateArray().OrderBy(p => p.TryGetProperty("index", out var i) ? i.GetInt32() : 0);
        foreach (var page in ordered)
        {
            if (!page.TryGetProperty("original", out var original) || !original.TryGetProperty("source", out var source)) continue;
            string url = source.GetString() ?? "";
            string file = await Download(http, url, folder, id, token);
            return (file, "Wikipedia · https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(page.GetProperty("title").GetString() ?? ""));
        }
        return null;
    }

    static async Task<string> Download(HttpClient http, string url, string folder, string id, CancellationToken token)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "upload.wikimedia.org") throw new InvalidDataException("Servidor de imagens não reconhecido.");
        string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) throw new InvalidDataException("Formato de imagem não suportado.");
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 15 * 1024 * 1024) throw new InvalidDataException("Imagem muito grande.");
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        if (bytes.Length > 15 * 1024 * 1024) throw new InvalidDataException("Imagem muito grande.");
        Directory.CreateDirectory(folder);
        foreach (string old in Directory.GetFiles(folder, id + ".*")) File.Delete(old);
        string file = Path.Combine(folder, id + ext);
        await File.WriteAllBytesAsync(file, bytes, token);
        return file;
    }
}
