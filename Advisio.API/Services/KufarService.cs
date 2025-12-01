using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public interface IKufarService
{
    Task LoadPlacesFromKufarAsync();
    Task LoadPlacesFromKvitkiAsync();
}

public class KufarService : IKufarService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KufarService>? _logger;

    public KufarService(
        AdvisioDbContext context,
        IPlaceService placeService,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<KufarService>? logger = null)
    {
        _context = context;
        _placeService = placeService;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task LoadPlacesFromKufarAsync()
    {
        try
        {
            // Kufar API для поиска развлечений и мероприятий
            var apiUrl = _configuration["ExternalApis:KufarApi"] ?? 
                        "https://api.kufar.by/search-api/v1/search/rendered-paginated";
            
            // Параметры запроса для поиска развлечений
            var queryParams = "?cat=1700&rgn=1&size=30&sort=lst.d"; // cat=1700 - развлечения
            var fullUrl = apiUrl + queryParams;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            var response = await _httpClient.GetStringAsync(fullUrl);
            var kufarData = JsonSerializer.Deserialize<KufarResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (kufarData?.Ads == null || !kufarData.Ads.Any())
                return;

            var categories = await _context.Categories.ToListAsync();

            int addedCount = 0;
            int updatedCount = 0;

            foreach (var ad in kufarData.Ads.Take(50)) // Ограничиваем до 50 объявлений
            {
                if (string.IsNullOrWhiteSpace(ad.Subject))
                    continue;

                var existingPlace = await _context.Places
                    .FirstOrDefaultAsync(p => p.Name == ad.Subject);

                var createPlaceDto = new CreatePlaceDto
                {
                    Name = ad.Subject ?? "Без названия",
                    Description = ad.Body ?? string.Empty,
                    Address = ad.Location?.Name,
                    ImageUrl = ad.Images?.FirstOrDefault()?.Url,
                    Latitude = ad.Location?.Latitude,
                    Longitude = ad.Location?.Longitude,
                    CategoryIds = DetermineCategories(ad, categories)
                };

                if (existingPlace != null)
                {
                    // Обновляем существующее место
                    existingPlace.Description = createPlaceDto.Description ?? existingPlace.Description;
                    existingPlace.Address = createPlaceDto.Address ?? existingPlace.Address;
                    existingPlace.ImageUrl = createPlaceDto.ImageUrl ?? existingPlace.ImageUrl;
                    existingPlace.Latitude = createPlaceDto.Latitude ?? existingPlace.Latitude;
                    existingPlace.Longitude = createPlaceDto.Longitude ?? existingPlace.Longitude;
                    existingPlace.UpdatedAt = DateTime.UtcNow;

                    // Обновляем категории
                    var existingCategoryIds = await _context.PlaceCategories
                        .Where(pc => pc.PlaceId == existingPlace.Id)
                        .Select(pc => pc.CategoryId)
                        .ToListAsync();

                    // Удаляем старые категории, которых нет в новых
                    var categoriesToRemove = existingCategoryIds.Except(createPlaceDto.CategoryIds).ToList();
                    foreach (var catId in categoriesToRemove)
                    {
                        var pc = await _context.PlaceCategories
                            .FirstOrDefaultAsync(pc => pc.PlaceId == existingPlace.Id && pc.CategoryId == catId);
                        if (pc != null)
                            _context.PlaceCategories.Remove(pc);
                    }

                    // Добавляем новые категории
                    var categoriesToAdd = createPlaceDto.CategoryIds.Except(existingCategoryIds).ToList();
                    foreach (var catId in categoriesToAdd)
                    {
                        _context.PlaceCategories.Add(new PlaceCategory
                        {
                            PlaceId = existingPlace.Id,
                            CategoryId = catId
                        });
                    }

                    await _context.SaveChangesAsync();
                    updatedCount++;
                }
                else
                {
                    // Создаем новое место
                    await _placeService.CreatePlaceAsync(createPlaceDto);
                    addedCount++;
                }
            }

            // Логируем результаты
            if (addedCount > 0 || updatedCount > 0)
            {
                _logger?.LogInformation("Kufar: Добавлено мест: {Added}, Обновлено мест: {Updated}", addedCount, updatedCount);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при загрузке мест из Kufar: {ex.Message}", ex);
        }
    }

    public async Task LoadPlacesFromKvitkiAsync()
    {
        try
        {
            var apiUrl = _configuration["ExternalApis:KvitkiApi"] ?? 
                        "https://kvitki.by/api/events";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await _httpClient.GetStringAsync(apiUrl);
            var kvitkiData = JsonSerializer.Deserialize<KvitkiResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (kvitkiData?.Events == null || !kvitkiData.Events.Any())
                return;

            var categories = await _context.Categories.ToListAsync();

            int addedCount = 0;
            int updatedCount = 0;

            foreach (var evt in kvitkiData.Events.Take(50))
            {
                if (string.IsNullOrWhiteSpace(evt.Title))
                    continue;

                var existingPlace = await _context.Places
                    .FirstOrDefaultAsync(p => p.Name == evt.Title);

                var createPlaceDto = new CreatePlaceDto
                {
                    Name = evt.Title ?? "Без названия",
                    Description = evt.Description ?? string.Empty,
                    Address = evt.Venue?.Name,
                    ImageUrl = evt.ImageUrl,
                    Latitude = evt.Venue?.Latitude,
                    Longitude = evt.Venue?.Longitude,
                    CategoryIds = DetermineCategoriesFromEvent(evt, categories)
                };

                if (existingPlace != null)
                {
                    // Обновляем существующее место
                    existingPlace.Description = createPlaceDto.Description ?? existingPlace.Description;
                    existingPlace.Address = createPlaceDto.Address ?? existingPlace.Address;
                    existingPlace.ImageUrl = createPlaceDto.ImageUrl ?? existingPlace.ImageUrl;
                    existingPlace.Latitude = createPlaceDto.Latitude ?? existingPlace.Latitude;
                    existingPlace.Longitude = createPlaceDto.Longitude ?? existingPlace.Longitude;
                    existingPlace.UpdatedAt = DateTime.UtcNow;

                    // Обновляем категории
                    var existingCategoryIds = await _context.PlaceCategories
                        .Where(pc => pc.PlaceId == existingPlace.Id)
                        .Select(pc => pc.CategoryId)
                        .ToListAsync();

                    // Удаляем старые категории, которых нет в новых
                    var categoriesToRemove = existingCategoryIds.Except(createPlaceDto.CategoryIds).ToList();
                    foreach (var catId in categoriesToRemove)
                    {
                        var pc = await _context.PlaceCategories
                            .FirstOrDefaultAsync(pc => pc.PlaceId == existingPlace.Id && pc.CategoryId == catId);
                        if (pc != null)
                            _context.PlaceCategories.Remove(pc);
                    }

                    // Добавляем новые категории
                    var categoriesToAdd = createPlaceDto.CategoryIds.Except(existingCategoryIds).ToList();
                    foreach (var catId in categoriesToAdd)
                    {
                        _context.PlaceCategories.Add(new PlaceCategory
                        {
                            PlaceId = existingPlace.Id,
                            CategoryId = catId
                        });
                    }

                    await _context.SaveChangesAsync();
                    updatedCount++;
                }
                else
                {
                    // Создаем новое место
                    await _placeService.CreatePlaceAsync(createPlaceDto);
                    addedCount++;
                }
            }

            // Логируем результаты
            if (addedCount > 0 || updatedCount > 0)
            {
                _logger?.LogInformation("Kvitki: Добавлено мест: {Added}, Обновлено мест: {Updated}", addedCount, updatedCount);
            }
            else
            {
                _logger?.LogInformation("Kvitki: Новых мест не найдено");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке мест из Kvitki");
            throw new Exception($"Ошибка при загрузке мест из Kvitki: {ex.Message}", ex);
        }
    }

    private List<int> DetermineCategories(KufarAd ad, List<Category> categories)
    {
        var categoryIds = new List<int>();

        // Определяем категории на основе описания и названия
        var text = (ad.Subject + " " + ad.Body).ToLower();

        // Активный отдых
        if (text.Contains("спорт") || text.Contains("фитнес") || text.Contains("актив"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("Активный отдых"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // Спокойный отдых
        if (text.Contains("музей") || text.Contains("театр") || text.Contains("выставка") || text.Contains("кино"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("Спокойный отдых"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // На природе
        if (text.Contains("парк") || text.Contains("природа") || text.Contains("лес") || text.Contains("озеро"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("На природе"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // По умолчанию добавляем категорию "С друзьями"
        var friendsCategory = categories.FirstOrDefault(c => c.Name.Contains("С друзьями"));
        if (friendsCategory != null && !categoryIds.Contains(friendsCategory.Id))
        {
            categoryIds.Add(friendsCategory.Id);
        }

        return categoryIds;
    }

    private List<int> DetermineCategoriesFromEvent(KvitkiEvent evt, List<Category> categories)
    {
        var categoryIds = new List<int>();

        var text = (evt.Title + " " + evt.Description).ToLower();
        var categoryName = evt.Category?.ToLower() ?? "";

        // Активный отдых
        if (categoryName.Contains("спорт") || text.Contains("спорт") || text.Contains("фитнес"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("Активный отдых"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // Спокойный отдых
        if (categoryName.Contains("культура") || categoryName.Contains("театр") || 
            text.Contains("музей") || text.Contains("театр") || text.Contains("выставка"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("Спокойный отдых"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // На природе
        if (text.Contains("парк") || text.Contains("природа"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("На природе"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // С семьей
        if (categoryName.Contains("семья") || text.Contains("семья") || text.Contains("дет"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("С семьей"));
            if (category != null) categoryIds.Add(category.Id);
        }
        else
        {
            // По умолчанию - с друзьями
            var friendsCategory = categories.FirstOrDefault(c => c.Name.Contains("С друзьями"));
            if (friendsCategory != null && !categoryIds.Contains(friendsCategory.Id))
            {
                categoryIds.Add(friendsCategory.Id);
            }
        }

        return categoryIds;
    }
}

// Модели для Kufar API
public class KufarResponse
{
    public List<KufarAd>? Ads { get; set; }
}

public class KufarAd
{
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public KufarLocation? Location { get; set; }
    public List<KufarImage>? Images { get; set; }
}

public class KufarLocation
{
    public string? Name { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class KufarImage
{
    public string? Url { get; set; }
}

// Модели для Kvitki API
public class KvitkiResponse
{
    public List<KvitkiEvent>? Events { get; set; }
}

public class KvitkiEvent
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }
    public KvitkiVenue? Venue { get; set; }
}

public class KvitkiVenue
{
    public string? Name { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

