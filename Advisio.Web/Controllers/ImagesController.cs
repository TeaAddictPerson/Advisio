using Microsoft.AspNetCore.Mvc;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

/// <summary>
/// Загрузка в wwwroot/uploads. Поиск картинок — через <see cref="IPlaceImageSearchService"/> (каскад как в API).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IPlaceImageSearchService _imageSearch;
    private readonly IWebHostEnvironment _env;

    public ImagesController(IPlaceImageSearchService imageSearch, IWebHostEnvironment env)
    {
        _imageSearch = imageSearch;
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
    public async Task<ActionResult<List<ImageSearchResult>>> Search(
        [FromQuery] string query,
        [FromQuery] int count = 8,
        [FromQuery] string? placeName = null,
        [FromQuery] string? placeType = null,
        [FromQuery] string? placeAddress = null,
        CancellationToken cancellationToken = default)
    {
        var q = PlaceImageSearchService.BuildKeywordQuery(query, placeName, placeType, placeAddress);
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest("Запрос должен содержать минимум 2 символа");

        count = Math.Clamp(count, 1, 30);

        var list = await _imageSearch.SearchAsync(query, placeName, placeType, placeAddress, count, cancellationToken);
        var dto = list.ConvertAll(x => new ImageSearchResult
        {
            Url = x.Url,
            ThumbnailUrl = x.ThumbnailUrl,
            Source = x.Source
        });
        return Ok(dto);
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
