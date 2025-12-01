using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public interface IExternalPlaceService
{
    Task LoadPlacesFromApiAsync(string apiUrl);
}

public class ExternalPlaceService : IExternalPlaceService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;
    private readonly HttpClient _httpClient;

    public ExternalPlaceService(
        AdvisioDbContext context,
        IPlaceService placeService,
        HttpClient httpClient)
    {
        _context = context;
        _placeService = placeService;
        _httpClient = httpClient;
    }

    public async Task LoadPlacesFromApiAsync(string apiUrl)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            var placesData = JsonSerializer.Deserialize<List<ExternalPlaceData>>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (placesData == null || !placesData.Any())
                return;

            // Получаем все существующие категории для маппинга
            var categories = await _context.Categories.ToListAsync();

            foreach (var placeData in placesData)
            {
                // Проверяем, не существует ли уже место с таким именем
                var existingPlace = await _context.Places
                    .FirstOrDefaultAsync(p => p.Name == placeData.Name);

                if (existingPlace != null)
                    continue;

                // Создаем место
                var createPlaceDto = new CreatePlaceDto
                {
                    Name = placeData.Name,
                    Description = placeData.Description ?? string.Empty,
                    Address = placeData.Address,
                    ImageUrl = placeData.ImageUrl,
                    Latitude = placeData.Latitude,
                    Longitude = placeData.Longitude,
                    CategoryIds = MapCategories(placeData.Categories, categories)
                };

                await _placeService.CreatePlaceAsync(createPlaceDto);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при загрузке мест из API: {ex.Message}", ex);
        }
    }

    private List<int> MapCategories(List<string>? categoryNames, List<Category> existingCategories)
    {
        if (categoryNames == null || !categoryNames.Any())
            return new List<int>();

        var categoryIds = new List<int>();

        foreach (var categoryName in categoryNames)
        {
            var category = existingCategories.FirstOrDefault(c => 
                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

            if (category != null)
            {
                categoryIds.Add(category.Id);
            }
        }

        return categoryIds;
    }
}

// Модель для данных из внешнего API (пример структуры)
public class ExternalPlaceData
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public List<string>? Categories { get; set; }
}

