using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace Advisio.API.Services;

public class PlaceImageSearchResult
{
    public string Url { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    public string? Source { get; set; }
}

public interface IPlaceImageSearchService
{
    Task<List<PlaceImageSearchResult>> SearchAsync(
        string query,
        string? placeName,
        string? placeType,
        string? placeAddress,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>Первая подходящая картинка (тот же каскад, что и в UI-поиске).</summary>
    Task<string?> PickFirstImageUrlAsync(
        string? placeName,
        string? placeType,
        string? placeAddress,
        CancellationToken cancellationToken = default);
}

public class PlaceImageSearchService : IPlaceImageSearchService
{
    private readonly IConfiguration _config;

    public PlaceImageSearchService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<string?> PickFirstImageUrlAsync(
        string? placeName,
        string? placeType,
        string? placeAddress,
        CancellationToken cancellationToken = default)
    {
        var list = await SearchAsync("", placeName, placeType, placeAddress, 1, cancellationToken);
        return list.Count > 0 ? list[0].Url : null;
    }

    public async Task<List<PlaceImageSearchResult>> SearchAsync(
        string query,
        string? placeName,
        string? placeType,
        string? placeAddress,
        int count,
        CancellationToken cancellationToken = default)
    {
        var q = BuildKeywordQuery(query, placeName, placeType, placeAddress);
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return new List<PlaceImageSearchResult>();

        count = Math.Clamp(count, 1, 30);

        var legacyProvider = (_config["ImageSearch:Provider"] ?? "").Trim();
        var legacyKey = _config["ImageSearch:ApiKey"];

        var unsplash = _config["ImageSearch:UnsplashApiKey"];
        if (string.IsNullOrEmpty(unsplash) && legacyProvider.Equals("unsplash", StringComparison.OrdinalIgnoreCase))
            unsplash = legacyKey;

        var pexels = _config["ImageSearch:PexelsApiKey"];
        if (string.IsNullOrEmpty(pexels) && legacyProvider.Equals("pexels", StringComparison.OrdinalIgnoreCase))
            pexels = legacyKey;

        var pixabay = _config["ImageSearch:PixabayApiKey"];

        var aggregated = new List<PlaceImageSearchResult>();
        void AddDistinct(IEnumerable<PlaceImageSearchResult> items)
        {
            foreach (var x in items)
            {
                if (string.IsNullOrEmpty(x.Url)) continue;
                if (aggregated.Any(a => string.Equals(a.Url, x.Url, StringComparison.OrdinalIgnoreCase))) continue;
                aggregated.Add(x);
                if (aggregated.Count >= count) break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        AddDistinct(await TryUnsplash(q, count, unsplash, cancellationToken));
        if (aggregated.Count < count)
            AddDistinct(await TryPexels(q, count - aggregated.Count, pexels, cancellationToken));
        if (aggregated.Count < count)
            AddDistinct(await TryPixabay(q, count - aggregated.Count, pixabay, cancellationToken));
        if (aggregated.Count < count)
            AddDistinct(await TryWikimediaCommonsAsync(q, count - aggregated.Count, cancellationToken));

        if (aggregated.Count == 0)
            return GetPlaceholderImages(q, count);

        return aggregated.Take(count).ToList();
    }

    public static string BuildKeywordQuery(string? userQuery, string? placeName, string? placeType, string? placeAddress)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddChunk(string? chunk, bool splitWords)
        {
            if (string.IsNullOrWhiteSpace(chunk)) return;
            var n = CollapseSpaces(chunk);
            if (splitWords)
            {
                foreach (var w in n.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = TrimEdgesPunctuation(w);
                    if (t.Length < 2) continue;
                    if (seen.Add(t))
                        tokens.Add(t);
                }
            }
            else
            {
                var t = TrimEdgesPunctuation(n);
                if (t.Length >= 2 && seen.Add(t))
                    tokens.Add(t);
            }
        }

        AddChunk(placeName, splitWords: true);
        AddChunk(placeType, splitWords: true);
        AddChunk(placeAddress, splitWords: true);
        if (!string.IsNullOrWhiteSpace(userQuery))
            AddChunk(userQuery, splitWords: true);

        return string.Join(' ', tokens);
    }

    private static string CollapseSpaces(string s) =>
        Regex.Replace(s.Trim(), @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static string TrimEdgesPunctuation(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        return Regex.Replace(word, @"^[\p{P}\p{S}\s]+|[\p{P}\p{S}\s]+$", "", RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    private static List<PlaceImageSearchResult> GetPlaceholderImages(string query, int count)
    {
        var results = new List<PlaceImageSearchResult>();
        var seed = query.GetHashCode() & 0x7FFFFFFF;
        for (int i = 0; i < count; i++)
        {
            var id = (seed + i * 100) % 1000;
            results.Add(new PlaceImageSearchResult
            {
                Url = $"https://picsum.photos/seed/{id}/800/600",
                ThumbnailUrl = $"https://picsum.photos/seed/{id}/400/300",
                Source = "picsum"
            });
        }
        return results;
    }

    private static async Task<List<PlaceImageSearchResult>> TryUnsplash(string query, int count, string? apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return new List<PlaceImageSearchResult>();
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Client-ID {apiKey}");
            var res = await client.GetAsync(
                $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(query)}&per_page={count}",
                cancellationToken);
            if (!res.IsSuccessStatusCode) return new List<PlaceImageSearchResult>();

            var json = await res.Content.ReadFromJsonAsync<UnsplashResponse>(cancellationToken: cancellationToken);
            if (json?.Results == null) return new List<PlaceImageSearchResult>();

            return json.Results.Select(r => new PlaceImageSearchResult
            {
                Url = r.Urls?.Regular ?? r.Urls?.Full ?? "",
                ThumbnailUrl = r.Urls?.Thumb ?? r.Urls?.Small,
                Source = "unsplash"
            }).Where(x => !string.IsNullOrEmpty(x.Url)).ToList();
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }
    }

    private static async Task<List<PlaceImageSearchResult>> TryPexels(string query, int count, string? apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return new List<PlaceImageSearchResult>();
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", apiKey);
            var res = await client.GetAsync(
                $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page={count}",
                cancellationToken);
            if (!res.IsSuccessStatusCode) return new List<PlaceImageSearchResult>();

            var json = await res.Content.ReadFromJsonAsync<PexelsResponse>(cancellationToken: cancellationToken);
            if (json?.Photos == null) return new List<PlaceImageSearchResult>();

            return json.Photos.Select(p => new PlaceImageSearchResult
            {
                Url = p.Src?.Large ?? p.Src?.Original ?? "",
                ThumbnailUrl = p.Src?.Medium ?? p.Src?.Small,
                Source = "pexels"
            }).Where(x => !string.IsNullOrEmpty(x.Url)).ToList();
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }
    }

    private static async Task<List<PlaceImageSearchResult>> TryPixabay(string query, int count, string? apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return new List<PlaceImageSearchResult>();
        try
        {
            using var client = new HttpClient();
            var url =
                $"https://pixabay.com/api/?key={Uri.EscapeDataString(apiKey)}&q={Uri.EscapeDataString(query)}&image_type=photo&per_page={count}&safesearch=true";
            var res = await client.GetAsync(url, cancellationToken);
            if (!res.IsSuccessStatusCode) return new List<PlaceImageSearchResult>();

            var json = await res.Content.ReadFromJsonAsync<PixabayResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
            if (json?.Hits == null) return new List<PlaceImageSearchResult>();

            return json.Hits.Select(h => new PlaceImageSearchResult
            {
                Url = h.LargeImageURL ?? h.WebformatURL ?? "",
                ThumbnailUrl = h.PreviewURL ?? h.WebformatURL,
                Source = "pixabay"
            }).Where(x => !string.IsNullOrEmpty(x.Url))
                .ToList();
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }
    }

    private async Task<List<PlaceImageSearchResult>> TryWikimediaCommonsAsync(string query, int needCount, CancellationToken cancellationToken)
    {
        needCount = Math.Clamp(needCount, 1, 30);
        var contact = _config["ImageSearch:HttpContactUrl"];
        if (string.IsNullOrWhiteSpace(contact))
            contact = "https://commons.wikimedia.org/wiki/Commons:Contact_us";

        var searchTerms = new List<string>();
        var trimmed = query.Trim();
        if (trimmed.Length > 0)
            searchTerms.Add(trimmed.Length > 280 ? trimmed[..280] : trimmed);
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 2)
            searchTerms.Add(string.Join(' ', parts.Take(2)));
        if (parts.Length > 0 && TrimEdgesPunctuation(parts[0]).Length >= 2)
            searchTerms.Add(TrimEdgesPunctuation(parts[0]));

        var distinctTerms = searchTerms.Distinct(StringComparer.Ordinal).ToList();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            $"Advisio-ImageSearch/1.0 ({contact})");

        var list = new List<PlaceImageSearchResult>();
        foreach (var term in distinctTerms)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (list.Count >= needCount) break;
            var batch = await WikimediaSearchBatchAsync(client, term, needCount - list.Count, cancellationToken);
            foreach (var item in batch)
            {
                if (list.Any(x => string.Equals(x.Url, item.Url, StringComparison.OrdinalIgnoreCase))) continue;
                list.Add(item);
                if (list.Count >= needCount) break;
            }
        }

        return list;
    }

    private static async Task<List<PlaceImageSearchResult>> WikimediaSearchBatchAsync(
        HttpClient client, string searchQuery, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 20);
        var searchUrl =
            "https://commons.wikimedia.org/w/api.php?action=query&format=json&list=search" +
            "&srnamespace=6" +
            $"&srlimit={limit}&srsearch={Uri.EscapeDataString(searchQuery)}";

        string raw;
        try
        {
            using var searchRes = await client.GetAsync(searchUrl, cancellationToken);
            if (!searchRes.IsSuccessStatusCode) return new List<PlaceImageSearchResult>();
            raw = await searchRes.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }

        List<string> titles;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out _))
                return new List<PlaceImageSearchResult>();
            if (!doc.RootElement.TryGetProperty("query", out var qEl) ||
                !qEl.TryGetProperty("search", out var searchArr) ||
                searchArr.ValueKind != JsonValueKind.Array)
                return new List<PlaceImageSearchResult>();

            titles = new List<string>();
            foreach (var item in searchArr.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var t))
                {
                    var title = t.GetString();
                    if (!string.IsNullOrEmpty(title))
                        titles.Add(title);
                }
            }
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }

        if (titles.Count == 0) return new List<PlaceImageSearchResult>();

        var titlesParam = string.Join('|', titles.Select(Uri.EscapeDataString));
        var infoUrl =
            "https://commons.wikimedia.org/w/api.php?action=query&format=json&prop=imageinfo" +
            "&iiprop=url&iiurlwidth=480" +
            $"&titles={titlesParam}";

        try
        {
            using var infoRes = await client.GetAsync(infoUrl, cancellationToken);
            if (!infoRes.IsSuccessStatusCode) return new List<PlaceImageSearchResult>();
            raw = await infoRes.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }

        var results = new List<PlaceImageSearchResult>();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("query", out var qEl) ||
                !qEl.TryGetProperty("pages", out var pagesEl) ||
                pagesEl.ValueKind != JsonValueKind.Object)
                return results;

            foreach (var page in pagesEl.EnumerateObject())
            {
                if (page.Value.TryGetProperty("missing", out _))
                    continue;
                if (!page.Value.TryGetProperty("imageinfo", out var ii) ||
                    ii.ValueKind != JsonValueKind.Array ||
                    ii.GetArrayLength() == 0)
                    continue;

                var imageInfo = ii[0];
                var url = imageInfo.TryGetProperty("url", out var uEl) ? uEl.GetString() : null;
                if (string.IsNullOrEmpty(url)) continue;

                var thumb = imageInfo.TryGetProperty("thumburl", out var thEl) ? thEl.GetString() : null;

                results.Add(new PlaceImageSearchResult
                {
                    Url = url,
                    ThumbnailUrl = thumb ?? url,
                    Source = "wikimedia"
                });
            }
        }
        catch
        {
            return new List<PlaceImageSearchResult>();
        }

        return results;
    }
}

internal class UnsplashResponse
{
    public List<UnsplashPhoto>? Results { get; set; }
}

internal class UnsplashPhoto
{
    public UnsplashUrls? Urls { get; set; }
}

internal class UnsplashUrls
{
    public string? Raw { get; set; }
    public string? Full { get; set; }
    public string? Regular { get; set; }
    public string? Small { get; set; }
    public string? Thumb { get; set; }
}

internal class PexelsResponse
{
    public List<PexelsPhoto>? Photos { get; set; }
}

internal class PexelsPhoto
{
    public PexelsSrc? Src { get; set; }
}

internal class PexelsSrc
{
    public string? Original { get; set; }
    public string? Large { get; set; }
    public string? Medium { get; set; }
    public string? Small { get; set; }
}

internal class PixabayResponse
{
    public List<PixabayHit>? Hits { get; set; }
}

internal class PixabayHit
{
    public string? PreviewURL { get; set; }
    public string? WebformatURL { get; set; }
    public string? LargeImageURL { get; set; }
}
