using Microsoft.AspNetCore.Mvc;

namespace Advisio.Web.Controllers;

/// <summary>
/// Поиск изображений для мест (можно интегрировать Unsplash, Pexels, Pixabay API).
/// Добавьте API ключ в appsettings.json: "ImageSearch": { "Provider": "unsplash", "ApiKey": "..." }
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public ImagesController(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImageUploadResult>> Upload([FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Файл не выбран" });
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
            return BadRequest(new { error = "Разрешены только изображения: JPG, PNG, GIF, WEBP" });

        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        try
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var url = $"/uploads/{fileName}";
            return Ok(new ImageUploadResult { Url = url });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка сохранения: {ex.Message}" });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<ImageSearchResult>>> Search([FromQuery] string query, [FromQuery] int count = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return BadRequest("Запрос должен содержать минимум 2 символа");

        var provider = _config["ImageSearch:Provider"] ?? "placeholder";
        var apiKey = _config["ImageSearch:ApiKey"];

        if (provider == "unsplash" && !string.IsNullOrEmpty(apiKey))
        {
            return await SearchUnsplash(query, count, apiKey);
        }

        if (provider == "pexels" && !string.IsNullOrEmpty(apiKey))
        {
            return await SearchPexels(query, count, apiKey);
        }

        // Placeholder: возвращаем изображения с picsum.photos по запросу
        return Ok(GetPlaceholderImages(query, count));
    }

    private static List<ImageSearchResult> GetPlaceholderImages(string query, int count)
    {
        var results = new List<ImageSearchResult>();
        var seed = query.GetHashCode() & 0x7FFFFFFF;
        for (int i = 0; i < count; i++)
        {
            var id = (seed + i * 100) % 1000;
            results.Add(new ImageSearchResult
            {
                Url = $"https://picsum.photos/seed/{id}/800/600",
                ThumbnailUrl = $"https://picsum.photos/seed/{id}/400/300",
                Source = "picsum"
            });
        }
        return results;
    }

    private async Task<ActionResult<List<ImageSearchResult>>> SearchUnsplash(string query, int count, string apiKey)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Client-ID {apiKey}");
            var res = await client.GetAsync($"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(query)}&per_page={count}");
            if (!res.IsSuccessStatusCode) return StatusCode((int)res.StatusCode);

            var json = await res.Content.ReadFromJsonAsync<UnsplashResponse>();
            if (json?.Results == null) return Ok(new List<ImageSearchResult>());

            return Ok(json.Results.Select(r => new ImageSearchResult
            {
                Url = r.Urls?.Regular ?? r.Urls?.Full,
                ThumbnailUrl = r.Urls?.Thumb ?? r.Urls?.Small,
                Source = "unsplash"
            }).Where(x => !string.IsNullOrEmpty(x.Url)).ToList());
        }
        catch
        {
            return Ok(GetPlaceholderImages(query, count));
        }
    }

    private async Task<ActionResult<List<ImageSearchResult>>> SearchPexels(string query, int count, string apiKey)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", apiKey);
            var res = await client.GetAsync($"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(query)}&per_page={count}");
            if (!res.IsSuccessStatusCode) return StatusCode((int)res.StatusCode);

            var json = await res.Content.ReadFromJsonAsync<PexelsResponse>();
            if (json?.Photos == null) return Ok(new List<ImageSearchResult>());

            return Ok(json.Photos.Select(p => new ImageSearchResult
            {
                Url = p.Src?.Large ?? p.Src?.Original,
                ThumbnailUrl = p.Src?.Medium ?? p.Src?.Small,
                Source = "pexels"
            }).Where(x => !string.IsNullOrEmpty(x.Url)).ToList());
        }
        catch
        {
            return Ok(GetPlaceholderImages(query, count));
        }
    }
}

public class ImageSearchResult
{
    public string Url { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    public string? Source { get; set; }
}

public class ImageUploadResult
{
    public string Url { get; set; } = "";
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
