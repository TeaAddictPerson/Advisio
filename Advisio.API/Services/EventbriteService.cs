using System.Text.Json;
using System.Net.Http;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public interface IEventbriteService
{
    Task LoadPlacesFromEventbriteAsync();
}

public class EventbriteService : IEventbriteService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventbriteService>? _logger;
    private readonly ITranslationService? _translationService;

    public EventbriteService(
        AdvisioDbContext context,
        IPlaceService placeService,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<EventbriteService>? logger = null,
        ITranslationService? translationService = null)
    {
        _context = context;
        _placeService = placeService;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _translationService = translationService;
    }

    public async Task LoadPlacesFromEventbriteAsync()
    {
        try
        {
            var apiToken = _configuration["Eventbrite:ApiToken"];
            if (string.IsNullOrEmpty(apiToken))
            {
                _logger?.LogError("Eventbrite API токен не настроен. Установите Eventbrite:ApiToken в appsettings.json");
                throw new Exception("Eventbrite API токен не настроен");
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            
            _logger?.LogInformation("🔑 Токен API настроен. Длина токена: {Length}", apiToken?.Length ?? 0);

            var categories = await _context.Categories.ToListAsync();
            int totalAddedCount = 0;
            int totalUpdatedCount = 0;
            int maxPlaces = _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 1000);

            // Eventbrite API: поиск публичных событий по городам
            // Endpoint /v3/events/search/ требует обязательный параметр location.address
            List<EventbriteEvent> allEvents = new List<EventbriteEvent>();
            
            _logger?.LogInformation("🔍 Ищем публичные события в Eventbrite API по координатам...");
            
            // Используем географические координаты вместо адресов
            // Структура: (name, lat, lon, radius)
            var cities = new[]
            {
                // Беларусь
                ("Minsk, Belarus", 53.9045, 27.5615, "50km"),
                ("Gomel, Belarus", 52.4412, 30.9878, "50km"),
                ("Mogilev, Belarus", 53.9000, 30.3333, "50km"),
                ("Vitebsk, Belarus", 55.1904, 30.2049, "50km"),
                ("Grodno, Belarus", 53.6884, 23.8258, "50km"),
                ("Brest, Belarus", 52.0975, 23.7341, "50km"),
                // Россия
                ("Moscow, Russia", 55.7558, 37.6173, "50km"),
                ("Saint Petersburg, Russia", 59.9343, 30.3351, "50km"),
                ("Novosibirsk, Russia", 55.0084, 82.9357, "50km"),
                ("Yekaterinburg, Russia", 56.8431, 60.6454, "50km"),
                ("Kazan, Russia", 55.8304, 49.0661, "50km"),
                ("Nizhny Novgorod, Russia", 56.2965, 43.9361, "50km"),
                ("Chelyabinsk, Russia", 55.1644, 61.4368, "50km"),
                ("Samara, Russia", 53.2001, 50.15, "50km"),
                ("Omsk, Russia", 54.9885, 73.3242, "50km"),
                ("Rostov-on-Don, Russia", 47.2357, 39.7015, "50km"),
                // Украина
                ("Kyiv, Ukraine", 50.4501, 30.5234, "50km"),
                ("Kharkiv, Ukraine", 49.9935, 36.2304, "50km"),
                ("Odesa, Ukraine", 46.4825, 30.7233, "50km"),
                ("Dnipro, Ukraine", 48.4647, 35.0462, "50km"),
                ("Lviv, Ukraine", 49.8397, 24.0297, "50km"),
                // Польша
                ("Warsaw, Poland", 52.2297, 21.0122, "50km"),
                ("Krakow, Poland", 50.0647, 19.9450, "50km"),
                ("Gdansk, Poland", 54.3520, 18.6466, "50km"),
                ("Wroclaw, Poland", 51.1079, 17.0385, "50km"),
                ("Poznan, Poland", 52.4064, 16.9252, "50km"),
                // Германия
                ("Berlin, Germany", 52.5200, 13.4050, "50km"),
                ("Munich, Germany", 48.1351, 11.5820, "50km"),
                ("Hamburg, Germany", 53.5511, 9.9937, "50km"),
                ("Frankfurt, Germany", 50.1109, 8.6821, "50km"),
                ("Cologne, Germany", 50.9375, 6.9603, "50km"),
                // Великобритания
                ("London, UK", 51.5074, -0.1278, "50km"),
                ("Manchester, UK", 53.4808, -2.2426, "50km"),
                ("Birmingham, UK", 52.4862, -1.8904, "50km"),
                ("Edinburgh, UK", 55.9533, -3.1883, "50km"),
                ("Glasgow, UK", 55.8642, -4.2518, "50km"),
                // Франция
                ("Paris, France", 48.8566, 2.3522, "50km"),
                ("Lyon, France", 45.7640, 4.8357, "50km"),
                ("Marseille, France", 43.2965, 5.3698, "50km"),
                ("Toulouse, France", 43.6047, 1.4442, "50km"),
                ("Nice, France", 43.7102, 7.2620, "50km"),
                // Испания
                ("Madrid, Spain", 40.4168, -3.7038, "50km"),
                ("Barcelona, Spain", 41.3851, 2.1734, "50km"),
                ("Valencia, Spain", 39.4699, -0.3763, "50km"),
                ("Seville, Spain", 37.3891, -5.9845, "50km"),
                ("Bilbao, Spain", 43.2627, -2.9253, "50km"),
                // Италия
                ("Rome, Italy", 41.9028, 12.4964, "50km"),
                ("Milan, Italy", 45.4642, 9.1900, "50km"),
                ("Naples, Italy", 40.8518, 14.2681, "50km"),
                ("Turin, Italy", 45.0703, 7.6869, "50km"),
                ("Florence, Italy", 43.7696, 11.2558, "50km"),
                // США
                ("New York, USA", 40.7128, -74.0060, "50km"),
                ("Los Angeles, USA", 34.0522, -118.2437, "50km"),
                ("Chicago, USA", 41.8781, -87.6298, "50km"),
                ("Houston, USA", 29.7604, -95.3698, "50km"),
                ("Phoenix, USA", 33.4484, -112.0740, "50km"),
                ("Philadelphia, USA", 39.9526, -75.1652, "50km"),
                ("San Antonio, USA", 29.4241, -98.4936, "50km"),
                ("San Diego, USA", 32.7157, -117.1611, "50km"),
                ("Dallas, USA", 32.7767, -96.7970, "50km"),
                ("San Jose, USA", 37.3382, -121.8863, "50km"),
                // Канада
                ("Toronto, Canada", 43.6532, -79.3832, "50km"),
                ("Montreal, Canada", 45.5017, -73.5673, "50km"),
                ("Vancouver, Canada", 49.2827, -123.1207, "50km"),
                ("Calgary, Canada", 51.0447, -114.0719, "50km"),
                ("Ottawa, Canada", 45.4215, -75.6972, "50km"),
                // Австралия
                ("Sydney, Australia", -33.8688, 151.2093, "50km"),
                ("Melbourne, Australia", -37.8136, 144.9631, "50km"),
                ("Brisbane, Australia", -27.4698, 153.0251, "50km"),
                ("Perth, Australia", -31.9505, 115.8605, "50km"),
                ("Adelaide, Australia", -34.9285, 138.6007, "50km"),
                // Азия
                ("Tokyo, Japan", 35.6762, 139.6503, "50km"),
                ("Seoul, South Korea", 37.5665, 126.9780, "50km"),
                ("Singapore", 1.3521, 103.8198, "50km"),
                ("Hong Kong", 22.3193, 114.1694, "50km"),
                ("Bangkok, Thailand", 13.7563, 100.5018, "50km"),
                ("Dubai, UAE", 25.2048, 55.2708, "50km"),
                ("Istanbul, Turkey", 41.0082, 28.9784, "50km"),
                ("Tel Aviv, Israel", 32.0853, 34.7818, "50km"),
                ("Mumbai, India", 19.0760, 72.8777, "50km"),
                ("Delhi, India", 28.6139, 77.2090, "50km"),
                ("Bangalore, India", 12.9716, 77.5946, "50km"),
                ("Shanghai, China", 31.2304, 121.4737, "50km"),
                ("Beijing, China", 39.9042, 116.4074, "50km"),
                ("Taipei, Taiwan", 25.0330, 121.5654, "50km"),
                ("Manila, Philippines", 14.5995, 120.9842, "50km"),
                // Латинская Америка
                ("Mexico City, Mexico", 19.4326, -99.1332, "50km"),
                ("São Paulo, Brazil", -23.5505, -46.6333, "50km"),
                ("Buenos Aires, Argentina", -34.6037, -58.3816, "50km"),
                ("Lima, Peru", -12.0464, -77.0428, "50km"),
                ("Bogotá, Colombia", 4.7110, -74.0721, "50km"),
                ("Santiago, Chile", -33.4489, -70.6693, "50km"),
                ("Rio de Janeiro, Brazil", -22.9068, -43.1729, "50km"),
                // Африка
                ("Cairo, Egypt", 30.0444, 31.2357, "50km"),
                ("Johannesburg, South Africa", -26.2041, 28.0473, "50km"),
                ("Lagos, Nigeria", 6.5244, 3.3792, "50km"),
                ("Nairobi, Kenya", -1.2921, 36.8219, "50km"),
                ("Casablanca, Morocco", 33.5731, -7.5898, "50km")
            };
            
            _logger?.LogInformation("🌍 Будем искать события в {CityCount} городах по координатам", cities.Length);
            
            // Тестируем endpoint с координатами
            var searchEndpoint = "https://www.eventbriteapi.com/v3/events/search";
            var testCity = cities[0]; // Первый город для теста
            
            _logger?.LogInformation("🧪 Тестируем endpoint с координатами: {City} ({Lat}, {Lon})", 
                testCity.Item1, testCity.Item2, testCity.Item3);
            
            try
            {
                var testParams = new Dictionary<string, string>
                {
                    { "location.latitude", testCity.Item2.ToString("F4") },
                    { "location.longitude", testCity.Item3.ToString("F4") },
                    { "location.within", testCity.Item4 },
                    { "status", "live" },
                    { "expand", "venue,description" },
                    { "page_size", "10" }
                };
                
                var testQueryString = string.Join("&", testParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                var testUrl = $"{searchEndpoint}?{testQueryString}";
                
                _logger?.LogInformation("🧪 Тестовый URL: {Url}", testUrl);
                
                var testResponse = await _httpClient.GetAsync(testUrl);
                var testContent = await testResponse.Content.ReadAsStringAsync();
                
                _logger?.LogInformation("🧪 Тестовый ответ: Status={Status}, Length={Length}", 
                    testResponse.StatusCode, testContent.Length);
                
                if (!testResponse.IsSuccessStatusCode)
                {
                    _logger?.LogError("❌ Endpoint с координатами вернул {StatusCode}: {Response}", 
                        testResponse.StatusCode, testContent.Substring(0, Math.Min(500, testContent.Length)));
                    
                    // Пробуем вариант со слэшем
                    searchEndpoint = "https://www.eventbriteapi.com/v3/events/search/";
                    testUrl = $"{searchEndpoint}?{testQueryString}";
                    _logger?.LogInformation("🧪 Пробуем вариант со слэшем: {Url}", testUrl);
                    
                    testResponse = await _httpClient.GetAsync(testUrl);
                    testContent = await testResponse.Content.ReadAsStringAsync();
                    
                    if (!testResponse.IsSuccessStatusCode)
                    {
                        _logger?.LogError("❌ Endpoint со слэшем также вернул {StatusCode}: {Response}", 
                            testResponse.StatusCode, testContent.Substring(0, Math.Min(500, testContent.Length)));
                        _logger?.LogError("❌ Endpoint /v3/events/search/ недоступен даже с координатами.");
                        return;
                    }
                    else
                    {
                        _logger?.LogInformation("✅ Endpoint со слэшем работает с координатами!");
                    }
                }
                else
                {
                    _logger?.LogInformation("✅ Endpoint без слэша работает с координатами!");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при тестировании endpoint с координатами");
                return;
            }
            
            int citiesProcessed = 0;
            int citiesWithEvents = 0;
            
            foreach (var (cityName, lat, lon, radius) in cities)
            {
                if (allEvents.Count >= maxPlaces)
                {
                    _logger?.LogInformation("✅ Достигнут лимит мест ({MaxPlaces}), останавливаем поиск", maxPlaces);
                    break;
                }
                
                citiesProcessed++;
                
                try
                {
                    var queryParams = new Dictionary<string, string>
                    {
                        { "location.latitude", lat.ToString("F4") },
                        { "location.longitude", lon.ToString("F4") },
                        { "location.within", radius },
                        { "status", "live" },
                        { "expand", "venue,description" },
                        { "page_size", "50" }
                    };
                    
                    if (citiesProcessed % 10 == 0 || citiesProcessed <= 5)
                    {
                        _logger?.LogInformation("🔍 [{Processed}/{Total}] Ищем события в городе: {City} ({Lat}, {Lon})", 
                            citiesProcessed, cities.Length, cityName, lat, lon);
                    }
                    
                    var cityEvents = await LoadEventsFromUrlAsync(searchEndpoint, queryParams, maxPlaces);
                    
                    if (cityEvents.Count > 0)
                    {
                        citiesWithEvents++;
                        
                        // Убираем дубликаты по названию
                        var newEvents = cityEvents.Where(e => !allEvents.Any(existing => 
                            existing.Name?.Text == e.Name?.Text)).ToList();
                        
                        allEvents.AddRange(newEvents);
                        
                        if (citiesProcessed % 10 == 0 || citiesProcessed <= 5 || newEvents.Count > 0)
                        {
                            _logger?.LogInformation("✅ Город {City}: получено {NewCount} новых событий (всего: {Total})", 
                                cityName, newEvents.Count, allEvents.Count);
                        }
                    }
                    
                    // Небольшая задержка между запросами, чтобы не превысить rate limit
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "❌ Ошибка при поиске событий в городе {City}", cityName);
                }
            }
            
            _logger?.LogInformation("🌍 Обработано городов: {Processed}/{Total}, с событиями: {WithEvents}, всего событий: {TotalEvents}", 
                citiesProcessed, cities.Length, citiesWithEvents, allEvents.Count);

            if (allEvents.Count == 0)
            {
                _logger?.LogWarning("⚠️ Не удалось получить события из Eventbrite API. Возможно, у пользователя нет созданных событий или токен не имеет доступа.");
                _logger?.LogInformation("ℹ️ Продолжаем работу без событий из Eventbrite");
                return;
            }

            _logger?.LogInformation("🎯 Всего получено {Count} событий из Eventbrite API", allEvents.Count);

            // Фильтруем только развлекательные события
            var eventsToProcess = new List<EventbriteEvent>();
            int filteredOut = 0;

            foreach (var evt in allEvents)
            {
                if (IsEntertainmentEvent(evt))
                {
                    eventsToProcess.Add(evt);
                }
                else
                {
                    filteredOut++;
                    if (filteredOut <= 5)
                    {
                        _logger?.LogDebug("❌ Отфильтровано событие: {Name}", evt.Name?.Text);
                    }
                }
            }

            _logger?.LogInformation("✅ Отфильтровано: {Filtered} развлекательных событий из {Total} (отклонено: {Rejected})", 
                eventsToProcess.Count, allEvents.Count, filteredOut);

            // Обрабатываем события
            int addedCount = 0;
            int updatedCount = 0;
            
            foreach (var evt in eventsToProcess)
            {
                if (string.IsNullOrWhiteSpace(evt.Name?.Text))
                {
                    continue;
                }

                var placeName = evt.Name.Text.Trim();
                if (placeName.Length > 200)
                {
                    placeName = placeName.Substring(0, 200);
                }

                // Формируем название с переводом на русский (переводим перед проверкой существующего места)
                var placeNameTranslated = placeName;
                if (!string.IsNullOrWhiteSpace(placeName) && _translationService != null)
                {
                    try
                    {
                        var nameToTranslate = placeName.Length > 200 ? placeName.Substring(0, 200) : placeName;
                        placeNameTranslated = await _translationService.TranslateToRussianAsync(nameToTranslate);
                        if (placeNameTranslated.Length > 200)
                        {
                            placeNameTranslated = placeNameTranslated.Substring(0, 200);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Ошибка при переводе названия '{Name}', используем оригинал", placeName);
                        placeNameTranslated = placeName;
                        if (placeNameTranslated.Length > 200)
                        {
                            placeNameTranslated = placeNameTranslated.Substring(0, 200);
                        }
                    }
                }
                else if (placeNameTranslated.Length > 200)
                {
                    placeNameTranslated = placeNameTranslated.Substring(0, 200);
                }

                // Проверяем существующее место по переведенному названию (case-insensitive)
                var existingPlace = await _context.Places
                    .FirstOrDefaultAsync(p => p.Name != null && p.Name.ToLower() == placeNameTranslated.ToLower());

                if (existingPlace != null)
                {
                    updatedCount++;
                    continue;
                }

                // Формируем описание с переводом на русский
                var description = evt.Description?.Text ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(description) && _translationService != null)
                {
                    try
                    {
                        var textToTranslate = description.Length > 1000 ? description.Substring(0, 1000) : description;
                        description = await _translationService.TranslateToRussianAsync(textToTranslate);
                        if (description.Length > 2000)
                        {
                            description = description.Substring(0, 2000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Ошибка при переводе описания, используем оригинал");
                        if (description.Length > 2000)
                        {
                            description = description.Substring(0, 2000);
                        }
                    }
                }
                else if (description.Length > 2000)
                {
                    description = description.Substring(0, 2000);
                }

                // Формируем адрес
                var address = evt.Venue?.Address?.LocalizedAreaDisplay ?? 
                              evt.Venue?.Name ?? 
                              string.Empty;
                if (!string.IsNullOrWhiteSpace(address) && _translationService != null)
                {
                    try
                    {
                        var addressToTranslate = address.Length > 200 ? address.Substring(0, 200) : address;
                        address = await _translationService.TranslateToRussianAsync(addressToTranslate);
                        if (address.Length > 200)
                        {
                            address = address.Substring(0, 200);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Ошибка при переводе адреса '{Address}', используем оригинал", address);
                        if (address.Length > 200)
                        {
                            address = address.Substring(0, 200);
                        }
                    }
                }
                else if (address.Length > 200)
                {
                    address = address.Substring(0, 200);
                }

                // Получаем координаты
                decimal? latitude = null;
                decimal? longitude = null;
                if (evt.Venue?.Latitude != null && evt.Venue.Longitude != null)
                {
                    latitude = (decimal)evt.Venue.Latitude.Value;
                    longitude = (decimal)evt.Venue.Longitude.Value;
                }

                // Получаем URL изображения
                var imageUrl = evt.Logo?.Url;
                if (imageUrl != null && imageUrl.Length > 500)
                {
                    imageUrl = imageUrl.Substring(0, 500);
                }

                var createPlaceDto = new CreatePlaceDto
                {
                    Name = placeNameTranslated,
                    Description = description,
                    Address = address,
                    ImageUrl = imageUrl,
                    Latitude = latitude,
                    Longitude = longitude,
                    CategoryIds = DetermineCategoriesFromEvent(evt, categories)
                };

                if (string.IsNullOrWhiteSpace(createPlaceDto.Name))
                {
                    continue;
                }

                try
                {
                    await _placeService.CreatePlaceAsync(createPlaceDto);
                    addedCount++;
                    totalAddedCount++;
                    
                    if (addedCount % 10 == 0 || addedCount <= 5)
                    {
                        _logger?.LogInformation("✅ ДОБАВЛЕНО место #{Count}: {Name}", addedCount, createPlaceDto.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "❌ Не удалось добавить место из Eventbrite: {Name} - {Error}", createPlaceDto.Name, ex.Message);
                }
            }

            totalUpdatedCount += updatedCount;

            _logger?.LogInformation("🎯 EVENTBRITE ИТОГО: ✅ ДОБАВЛЕНО: {Added} мест, ⏭️ ПРОПУЩЕНО: {Updated} мест", 
                totalAddedCount, totalUpdatedCount);
            
            if (totalAddedCount == 0)
            {
                _logger?.LogWarning("⚠️ Не добавлено ни одного нового места из Eventbrite. Возможно, все места уже есть в БД.");
            }
            else
            {
                _logger?.LogInformation("🎉 УСПЕХ: Добавлено {Count} новых мест в БД из Eventbrite!", totalAddedCount);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке мест из Eventbrite");
            throw new Exception($"Ошибка при загрузке мест из Eventbrite: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Загружает события из указанного URL с пагинацией
    /// </summary>
    private async Task<List<EventbriteEvent>> LoadEventsFromUrlAsync(string baseUrl, Dictionary<string, string>? customParams = null, int maxEvents = 1000)
    {
        var allEvents = new List<EventbriteEvent>();
        var queryParams = customParams ?? new Dictionary<string, string>
        {
            { "status", "live" },
            { "expand", "venue,description" },
            { "page_size", "50" }
        };
        
        string? continuationToken = null;
        int page = 1;
        int maxPages = 20;
        bool hasMore = true;

        while (hasMore && page <= maxPages && allEvents.Count < maxEvents)
        {
            var currentParams = new Dictionary<string, string>(queryParams);
            
            if (!string.IsNullOrEmpty(continuationToken))
            {
                currentParams["continuation"] = continuationToken;
            }
            
            var queryString = string.Join("&", currentParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
            var fullUrl = $"{baseUrl}?{queryString}";

            _logger?.LogInformation("📄 Загрузка страницы {Page} из {Url}", page, baseUrl);

            try
            {
                var httpResponse = await _httpClient.GetAsync(fullUrl);
                var response = await httpResponse.Content.ReadAsStringAsync();
                
                if (page == 1)
                {
                    _logger?.LogInformation("Eventbrite API ответ: Status={Status}, ContentLength={Length}", 
                        httpResponse.StatusCode, response.Length);
                    
                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        _logger?.LogWarning("⚠️ Eventbrite API вернул ошибку {StatusCode}: {Response}", 
                            httpResponse.StatusCode, response.Substring(0, Math.Min(500, response.Length)));
                        
                        if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            // Для поиска по городам 404 означает, что в этом городе нет событий - это нормально
                            _logger?.LogDebug("ℹ️ События не найдены в этом городе (404). Продолжаем поиск.");
                            hasMore = false;
                            break;
                        }
                        
                        hasMore = false;
                        break;
                    }
                }
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("⚠️ Eventbrite API вернул ошибку {StatusCode}", httpResponse.StatusCode);
                    hasMore = false;
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(response))
                {
                    _logger?.LogWarning("Пустой ответ от Eventbrite API для страницы {Page}", page);
                    hasMore = false;
                    break;
                }

                var eventbriteData = JsonSerializer.Deserialize<EventbriteResponse>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (eventbriteData == null)
                {
                    _logger?.LogError("Не удалось десериализовать ответ Eventbrite API");
                    hasMore = false;
                    break;
                }

                if (eventbriteData.Events == null || !eventbriteData.Events.Any())
                {
                    _logger?.LogInformation("Нет данных на странице {Page}", page);
                    hasMore = false;
                    break;
                }

                var pageEvents = eventbriteData.Events;
                allEvents.AddRange(pageEvents);
                _logger?.LogInformation("✅ Страница {Page}: получено {Count} событий (всего собрано: {Total})", 
                    page, pageEvents.Count, allEvents.Count);

                continuationToken = eventbriteData.Pagination?.Continuation;
                
                if (pageEvents.Count < 50 || eventbriteData.Pagination?.HasMoreItems == false)
                {
                    hasMore = false;
                }
                else
                {
                    page++;
                    if (page > maxPages || string.IsNullOrEmpty(continuationToken))
                    {
                        hasMore = false;
                    }
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при загрузке страницы {Page} из Eventbrite", page);
                hasMore = false;
                break;
            }
        }

        return allEvents;
    }

    private List<int> DetermineCategoriesFromEvent(EventbriteEvent evt, List<Category> categories)
    {
        var categoryIds = new List<int>();

        var text = ((evt.Name?.Text ?? "") + " " + (evt.Description?.Text ?? "") + " " + (evt.Category?.Name ?? "")).ToLower();
        var categoryName = evt.Category?.Name?.ToLower() ?? "";

        // Активный отдых
        if (categoryName.Contains("sport") || categoryName.Contains("fitness") || 
            text.Contains("спорт") || text.Contains("фитнес") || text.Contains("актив"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("Активный отдых"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // Спокойный отдых
        if (categoryName.Contains("arts") || categoryName.Contains("music") || categoryName.Contains("film") ||
            text.Contains("музей") || text.Contains("театр") || text.Contains("выставка") || 
            text.Contains("концерт") || text.Contains("кино"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("Спокойный отдых"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // На природе
        if (text.Contains("парк") || text.Contains("природа") || text.Contains("outdoor"))
        {
            var category = categories.FirstOrDefault(c => c.Name.Contains("На природе"));
            if (category != null) categoryIds.Add(category.Id);
        }

        // С семьей
        if (categoryName.Contains("family") || text.Contains("семья") || text.Contains("дет") || text.Contains("family"))
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

    /// <summary>
    /// Проверяет, является ли событие развлечением
    /// </summary>
    private bool IsEntertainmentEvent(EventbriteEvent evt)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.Name?.Text))
        {
            return false;
        }

        var text = ((evt.Name?.Text ?? "") + " " + (evt.Description?.Text ?? "") + " " + (evt.Category?.Name ?? "")).ToLower();
        
        // Ключевые слова развлечений
        var entertainmentKeywords = new[]
        {
            "concert", "show", "theater", "theatre", "circus", "festival",
            "entertainment", "leisure", "quest", "attraction", "cinema", "movie", "film",
            "exhibition", "museum", "gallery", "club", "bar", "restaurant", "cafe",
            "disco", "dance", "music", "comedy", "humor", "game",
            "event", "party", "celebration", "carnival",
            "entertaining", "cultural", "creative",
            "tour", "excursion", "master class", "workshop", "seminar",
            "sport", "fitness", "yoga", "bowling", "billiards",
            "waterpark", "amusement park", "children center",
            // Русские ключевые слова
            "концерт", "шоу", "спектакль", "театр", "цирк", "фестиваль",
            "развлечение", "досуг", "квест", "аттракцион", "кино", "фильм",
            "выставка", "музей", "галерея", "клуб", "бар", "ресторан", "кафе",
            "дискотека", "танцы", "музыка", "комедия", "юмор", "игра",
            "мероприятие", "событие", "вечеринка", "праздник", "карнавал",
            "развлекательный", "досуговый", "культурный", "творческий",
            "экскурсия", "тур", "мастер-класс", "тренинг", "семинар",
            "спорт", "фитнес", "йога", "боулинг", "бильярд",
            "аквапарк", "парк развлечений", "детский центр"
        };

        // Исключения - если есть эти слова, событие НЕ подходит
        var excludeKeywords = new[]
        {
            "work", "job", "vacancy", "resume", "employment", "hire",
            "education", "course", "school", "university", "college", "institute",
            "treatment", "medicine", "doctor", "hospital", "clinic", "diagnosis", "therapy",
            "repair", "construction", "finishing", "interior design", "installation",
            "service", "delivery", "transportation", "taxi", "freight",
            "real estate", "apartment", "house", "dacha", "plot", "property",
            "car", "vehicle", "auto", "parts", "auto service",
            "clothing", "shoes", "accessories", "cosmetics", "shop",
            // Русские исключения
            "работа", "вакансия", "резюме", "трудоустройство", "найм",
            "обучение", "курсы", "школа", "университет", "колледж", "институт",
            "лечение", "медицина", "врач", "больница", "клиника", "диагностика", "терапия",
            "ремонт", "строительство", "отделка", "дизайн интерьера", "монтаж",
            "услуги", "сервис", "доставка", "перевозка", "такси", "грузоперевозки",
            "недвижимость", "квартира", "дом", "дача", "участок", "продажа квартир",
            "автомобиль", "машина", "транспорт", "запчасти", "автосервис",
            "одежда", "обувь", "аксессуары", "косметика", "магазин"
        };

        // Проверяем наличие развлечений
        bool hasEntertainmentKeyword = entertainmentKeywords.Any(keyword => text.Contains(keyword));
        
        // Проверяем исключения
        bool hasExcludeKeyword = excludeKeywords.Any(keyword => text.Contains(keyword));
        
        if (hasExcludeKeyword)
        {
            return false;
        }

        return hasEntertainmentKeyword;
    }
}

// Модели для Eventbrite API
public class EventbriteResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("events")]
    public List<EventbriteEvent>? Events { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("pagination")]
    public EventbritePagination? Pagination { get; set; }
}

public class EventbriteEvent
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public EventbriteText? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public EventbriteText? Description { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("venue")]
    public EventbriteVenue? Venue { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("logo")]
    public EventbriteLogo? Logo { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("category")]
    public EventbriteCategory? Category { get; set; }
}

public class EventbriteText
{
    [System.Text.Json.Serialization.JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class EventbriteVenue
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("address")]
    public EventbriteAddress? Address { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("latitude")]
    public double? Latitude { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
}

public class EventbriteAddress
{
    [System.Text.Json.Serialization.JsonPropertyName("localized_area_display")]
    public string? LocalizedAreaDisplay { get; set; }
}

public class EventbriteLogo
{
    [System.Text.Json.Serialization.JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class EventbriteCategory
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class EventbritePagination
{
    [System.Text.Json.Serialization.JsonPropertyName("has_more_items")]
    public bool? HasMoreItems { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("continuation")]
    public string? Continuation { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("page_count")]
    public int? PageCount { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("page_number")]
    public int? PageNumber { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("page_size")]
    public int? PageSize { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("object_count")]
    public int? ObjectCount { get; set; }
}

public class EventbriteOrganizationsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("organizations")]
    public List<EventbriteOrganization>? Organizations { get; set; }
}

public class EventbriteOrganization
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
}
