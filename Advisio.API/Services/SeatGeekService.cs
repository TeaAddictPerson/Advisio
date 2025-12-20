using System.Text.Json;
using System.Net.Http;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public interface ISeatGeekService
{
    Task LoadPlacesFromSeatGeekAsync();
}

public class SeatGeekService : ISeatGeekService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeatGeekService>? _logger;
    private readonly ITranslationService? _translationService;

    public SeatGeekService(
        AdvisioDbContext context,
        IPlaceService placeService,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SeatGeekService>? logger = null,
        ITranslationService? translationService = null)
    {
        _context = context;
        _placeService = placeService;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _translationService = translationService;
    }

    public async Task LoadPlacesFromSeatGeekAsync()
    {
        try
        {
            var clientId = _configuration["SeatGeek:ClientId"];
            if (string.IsNullOrEmpty(clientId))
            {
                _logger?.LogError("SeatGeek API Client ID не настроен. Установите SeatGeek:ClientId в appsettings.json");
                throw new Exception("SeatGeek API Client ID не настроен");
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.BaseAddress = new Uri("https://api.seatgeek.com/2/");
            
            // SeatGeek API требует Client ID в каждом запросе как query параметр
            // Добавляем полный набор браузерных заголовков для обхода блокировки Varnish
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,ru;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://seatgeek.com/");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://seatgeek.com");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            
            // Проверяем, используется ли прокси
            var proxyUrl = _configuration["SeatGeek:ProxyUrl"];
            var useSystemProxy = string.IsNullOrEmpty(proxyUrl);
            
            _logger?.LogInformation("✅ SeatGeek API настроен. Client ID: {ClientId}", 
                string.IsNullOrEmpty(clientId) ? "НЕ НАСТРОЕН" : $"{clientId.Substring(0, Math.Min(20, clientId.Length))}...");
            
            if (useSystemProxy)
            {
                _logger?.LogInformation("🌐 Используется системный прокси (VPN должен быть активен на уровне системы)");
                _logger?.LogWarning("⚠️ ВАЖНО: Браузерные VPN (Urban VPN, Hola и т.д.) НЕ РАБОТАЮТ для приложений!");
                _logger?.LogWarning("⚠️ Нужен системный VPN или настройте ProxyUrl в appsettings.json");
                _logger?.LogWarning("⚠️ Пример настройки прокси в appsettings.json:");
                _logger?.LogWarning("   \"SeatGeek\": {");
                _logger?.LogWarning("     \"ProxyUrl\": \"http://proxy.example.com:8080\"");
                _logger?.LogWarning("   }");
            }
            else
            {
                _logger?.LogInformation("🌐 Используется прокси: {ProxyUrl}", proxyUrl);
            }

            _logger?.LogInformation("🔍 Ищем публичные события в SeatGeek API по координатам...");
            
            // Сначала делаем простой тестовый запрос для проверки Client ID
            _logger?.LogInformation("🧪 Тестируем Client ID с простым запросом...");
            try
            {
                var testUrl = $"events?client_id={Uri.EscapeDataString(clientId)}&per_page=1";
                var testResponse = await _httpClient.GetAsync(testUrl);
                var testContent = await testResponse.Content.ReadAsStringAsync();
                
                if (testResponse.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("✅ Тестовый запрос успешен! Client ID работает. Status: {Status}", testResponse.StatusCode);
                }
                else
                {
                    _logger?.LogWarning("⚠️ Тестовый запрос вернул {Status}: {Content}", 
                        testResponse.StatusCode, testContent.Substring(0, Math.Min(500, testContent.Length)));
                    
                    if (testResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        _logger?.LogError("❌ Client ID заблокирован или неверен. Проверьте Client ID в панели SeatGeek.");
                        _logger?.LogError("❌ Возможно, нужно активировать Client ID или проверить права доступа.");
                        return; // Останавливаем выполнение, если Client ID не работает
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при тестовом запросе: {Message}", ex.Message);
                // Продолжаем выполнение, но логируем ошибку
            }

            // Список крупных городов мира с координатами и радиусом (в милях для SeatGeek)
            var cities = new[]
            {
                // Беларусь
                ("Minsk, Belarus", 53.9045, 27.5615, 30),
                ("Gomel, Belarus", 52.4412, 30.9878, 30),
                ("Mogilev, Belarus", 53.9000, 30.3333, 30),
                ("Vitebsk, Belarus", 55.1904, 30.2049, 30),
                ("Grodno, Belarus", 53.6884, 23.8258, 30),
                ("Brest, Belarus", 52.0975, 23.7341, 30),
                // Россия
                ("Moscow, Russia", 55.7558, 37.6173, 30),
                ("Saint Petersburg, Russia", 59.9343, 30.3351, 30),
                ("Novosibirsk, Russia", 55.0084, 82.9357, 30),
                ("Yekaterinburg, Russia", 56.8431, 60.6454, 30),
                ("Kazan, Russia", 55.8304, 49.0661, 30),
                ("Nizhny Novgorod, Russia", 56.2965, 43.9361, 30),
                ("Chelyabinsk, Russia", 55.1644, 61.4368, 30),
                ("Samara, Russia", 53.2001, 50.15, 30),
                ("Omsk, Russia", 54.9885, 73.3242, 30),
                ("Rostov-on-Don, Russia", 47.2357, 39.7015, 30),
                // Украина
                ("Kyiv, Ukraine", 50.4501, 30.5234, 30),
                ("Kharkiv, Ukraine", 49.9935, 36.2304, 30),
                ("Odesa, Ukraine", 46.4825, 30.7233, 30),
                ("Dnipro, Ukraine", 48.4647, 35.0462, 30),
                ("Lviv, Ukraine", 49.8397, 24.0297, 30),
                // Польша
                ("Warsaw, Poland", 52.2297, 21.0122, 30),
                ("Krakow, Poland", 50.0647, 19.9450, 30),
                ("Gdansk, Poland", 54.3520, 18.6466, 30),
                ("Wroclaw, Poland", 51.1079, 17.0385, 30),
                ("Poznan, Poland", 52.4064, 16.9252, 30),
                // Германия
                ("Berlin, Germany", 52.5200, 13.4050, 30),
                ("Munich, Germany", 48.1351, 11.5820, 30),
                ("Hamburg, Germany", 53.5511, 9.9937, 30),
                ("Frankfurt, Germany", 50.1109, 8.6821, 30),
                ("Cologne, Germany", 50.9375, 6.9603, 30),
                // Великобритания
                ("London, UK", 51.5074, -0.1278, 30),
                ("Manchester, UK", 53.4808, -2.2426, 30),
                ("Birmingham, UK", 52.4862, -1.8904, 30),
                ("Edinburgh, UK", 55.9533, -3.1883, 30),
                ("Glasgow, UK", 55.8642, -4.2518, 30),
                // Франция
                ("Paris, France", 48.8566, 2.3522, 30),
                ("Lyon, France", 45.7640, 4.8357, 30),
                ("Marseille, France", 43.2965, 5.3698, 30),
                ("Toulouse, France", 43.6047, 1.4442, 30),
                ("Nice, France", 43.7102, 7.2620, 30),
                // Испания
                ("Madrid, Spain", 40.4168, -3.7038, 30),
                ("Barcelona, Spain", 41.3851, 2.1734, 30),
                ("Valencia, Spain", 39.4699, -0.3763, 30),
                ("Seville, Spain", 37.3891, -5.9845, 30),
                ("Bilbao, Spain", 43.2627, -2.9253, 30),
                // Италия
                ("Rome, Italy", 41.9028, 12.4964, 30),
                ("Milan, Italy", 45.4642, 9.1900, 30),
                ("Naples, Italy", 40.8518, 14.2681, 30),
                ("Turin, Italy", 45.0703, 7.6869, 30),
                ("Florence, Italy", 43.7696, 11.2558, 30),
                // США
                ("New York, USA", 40.7128, -74.0060, 30),
                ("Los Angeles, USA", 34.0522, -118.2437, 30),
                ("Chicago, USA", 41.8781, -87.6298, 30),
                ("Houston, USA", 29.7604, -95.3698, 30),
                ("Phoenix, USA", 33.4484, -112.0740, 30),
                ("Philadelphia, USA", 39.9526, -75.1652, 30),
                ("San Antonio, USA", 29.4241, -98.4936, 30),
                ("San Diego, USA", 32.7157, -117.1611, 30),
                ("Dallas, USA", 32.7767, -96.7970, 30),
                ("San Jose, USA", 37.3382, -121.8863, 30),
                // Канада
                ("Toronto, Canada", 43.6532, -79.3832, 30),
                ("Montreal, Canada", 45.5017, -73.5673, 30),
                ("Vancouver, Canada", 49.2827, -123.1207, 30),
                ("Calgary, Canada", 51.0447, -114.0719, 30),
                ("Ottawa, Canada", 45.4215, -75.6972, 30),
                // Австралия
                ("Sydney, Australia", -33.8688, 151.2093, 30),
                ("Melbourne, Australia", -37.8136, 144.9631, 30),
                ("Brisbane, Australia", -27.4698, 153.0251, 30),
                ("Perth, Australia", -31.9505, 115.8605, 30),
                ("Adelaide, Australia", -34.9285, 138.6007, 30),
                // Азия
                ("Tokyo, Japan", 35.6762, 139.6503, 30),
                ("Seoul, South Korea", 37.5665, 126.9780, 30),
                ("Singapore", 1.3521, 103.8198, 30),
                ("Hong Kong", 22.3193, 114.1694, 30),
                ("Bangkok, Thailand", 13.7563, 100.5018, 30),
                ("Dubai, UAE", 25.2048, 55.2708, 30),
                ("Istanbul, Turkey", 41.0082, 28.9784, 30),
                ("Tel Aviv, Israel", 32.0853, 34.7818, 30),
                ("Mumbai, India", 19.0760, 72.8777, 30),
                ("Delhi, India", 28.6139, 77.2090, 30),
                ("Bangalore, India", 12.9716, 77.5946, 30),
                ("Shanghai, China", 31.2304, 121.4737, 30),
                ("Beijing, China", 39.9042, 116.4074, 30),
                ("Taipei, Taiwan", 25.0330, 121.5654, 30),
                ("Manila, Philippines", 14.5995, 120.9842, 30),
                // Латинская Америка
                ("Mexico City, Mexico", 19.4326, -99.1332, 30),
                ("São Paulo, Brazil", -23.5505, -46.6333, 30),
                ("Buenos Aires, Argentina", -34.6037, -58.3816, 30),
                ("Lima, Peru", -12.0464, -77.0428, 30),
                ("Bogotá, Colombia", 4.7110, -74.0721, 30),
                ("Santiago, Chile", -33.4489, -70.6693, 30),
                ("Rio de Janeiro, Brazil", -22.9068, -43.1729, 30),
                // Африка
                ("Cairo, Egypt", 30.0444, 31.2357, 30),
                ("Johannesburg, South Africa", -26.2041, 28.0473, 30),
                ("Lagos, Nigeria", 6.5244, 3.3792, 30),
                ("Nairobi, Kenya", -1.2921, 36.8219, 30),
                ("Casablanca, Morocco", 33.5731, -7.5898, 30)
            };

            _logger?.LogInformation("🌍 Будем искать события в {CityCount} городах по координатам", cities.Length);

            var maxPlaces = _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 2000);
            List<SeatGeekEvent> allEvents = new List<SeatGeekEvent>();
            int citiesProcessed = 0;
            int citiesWithEvents = 0;

            foreach (var (cityName, lat, lon, radiusMiles) in cities)
            {
                if (allEvents.Count >= maxPlaces)
                {
                    _logger?.LogInformation("✅ Достигнут лимит мест ({MaxPlaces}), останавливаем поиск", maxPlaces);
                    break;
                }

                citiesProcessed++;

                try
                {
                    // SeatGeek API: https://api.seatgeek.com/2/events?lat=40.7128&lon=-74.0060&radius=30&client_id=YOUR_CLIENT_ID
                    // Используем точку для десятичных чисел (InvariantCulture)
                    // Формируем query параметры для SeatGeek API
                    // ВАЖНО: Client ID должен быть в каждом запросе
                    var queryParams = new Dictionary<string, string>
                    {
                        { "lat", lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) },
                        { "lon", lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) },
                        { "radius", radiusMiles.ToString() },
                        { "client_id", clientId },
                        { "per_page", "50" }, // Уменьшаем размер страницы для меньшей нагрузки
                        { "page", "1" }
                    };

                    var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                    var url = $"events?{queryString}";
                    
                    // Логируем URL для первого города для отладки
                    if (citiesProcessed == 1)
                    {
                        _logger?.LogInformation("🧪 Тестовый запрос: {BaseUrl}{Url}", _httpClient.BaseAddress, url);
                    }

                    if (citiesProcessed % 10 == 0 || citiesProcessed <= 5)
                    {
                        _logger?.LogInformation("🔍 [{Processed}/{Total}] Ищем события в городе: {City} ({Lat}, {Lon})", 
                            citiesProcessed, cities.Length, cityName, lat, lon);
                    }

                    HttpResponseMessage? httpResponse = null;
                    string response = "";
                    
                    try
                    {
                        httpResponse = await _httpClient.GetAsync(url);
                        response = await httpResponse.Content.ReadAsStringAsync();
                        
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            if (httpResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                _logger?.LogWarning("⚠️ SeatGeek API вернул 403 Forbidden для города {City}. Возможные причины:", cityName);
                                _logger?.LogWarning("   1. Client ID неверный или неактивен");
                                _logger?.LogWarning("   2. Превышен rate limit");
                                _logger?.LogWarning("   3. IP адрес заблокирован Varnish");
                                _logger?.LogWarning("   4. Недостаточно браузерных заголовков");
                                
                                // Логируем первые 500 символов ответа для диагностики
                                if (response.Length > 0)
                                {
                                    _logger?.LogWarning("   Ответ: {Response}", response.Substring(0, Math.Min(500, response.Length)));
                                }
                                
                                // Если это первые 3 города и все возвращают 403, останавливаем
                                if (citiesProcessed <= 3)
                                {
                                    _logger?.LogError("❌ Первые {Count} города вернули 403 Forbidden. Проверьте Client ID и настройки.", citiesProcessed);
                                    if (citiesProcessed == 3)
                                    {
                                        _logger?.LogError("❌ Остановка всех запросов из-за множественных 403 Forbidden. Проверьте Client ID.");
                                        return;
                                    }
                                }
                                else
                                {
                                    // Для остальных городов просто пропускаем с задержкой
                                    _logger?.LogWarning("⚠️ Пропускаем город {City} из-за 403", cityName);
                                    await Task.Delay(1000); // Увеличиваем задержку при ошибках
                                }
                            }
                            else
                            {
                                _logger?.LogWarning("⚠️ SeatGeek API вернул {StatusCode} для города {City}: {Response}", 
                                    httpResponse.StatusCode, cityName, response.Substring(0, Math.Min(200, response.Length)));
                            }
                            continue;
                        }
                        
                        if (citiesProcessed <= 3)
                        {
                            _logger?.LogDebug("📥 Ответ SeatGeek API (первые 500 символов): {Response}", 
                                response.Substring(0, Math.Min(500, response.Length)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "❌ Ошибка HTTP запроса к SeatGeek API для города {City}", cityName);
                        continue;
                    }
                    
                    SeatGeekResponse? data = null;
                    try
                    {
                        data = JsonSerializer.Deserialize<SeatGeekResponse>(response, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "❌ Ошибка десериализации ответа SeatGeek API для города {City}", cityName);
                        continue;
                    }
                    
                    if (data == null)
                    {
                        _logger?.LogWarning("⚠️ Не удалось десериализовать ответ SeatGeek API для города {City}", cityName);
                        continue;
                    }

                    if (data?.Events != null && data.Events.Any())
                    {
                        citiesWithEvents++;
                        var newEvents = data.Events.Where(e => !allEvents.Any(existing => 
                            existing.Id == e.Id)).ToList();
                        
                        allEvents.AddRange(newEvents);

                        if (citiesProcessed % 10 == 0 || citiesProcessed <= 5 || newEvents.Count > 0)
                        {
                            _logger?.LogInformation("✅ Город {City}: получено {NewCount} новых событий (всего: {Total})", 
                                cityName, newEvents.Count, allEvents.Count);
                        }

                        // Обрабатываем пагинацию, если есть
                        int page = 2;
                        int maxPages = 10;
                        while (page <= maxPages && allEvents.Count < maxPlaces && data.Meta?.HasMore == true)
                        {
                            queryParams["page"] = page.ToString();
                            queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                            url = $"events?{queryString}";

                            response = await _httpClient.GetStringAsync(url);
                            data = JsonSerializer.Deserialize<SeatGeekResponse>(response, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (data?.Events != null && data.Events.Any())
                            {
                                var pageEvents = data.Events.Where(e => !allEvents.Any(existing => 
                                    existing.Id == e.Id)).ToList();
                                allEvents.AddRange(pageEvents);
                            }
                            else
                            {
                                break;
                            }

                            page++;
                            await Task.Delay(200); // Задержка между страницами
                        }
                    }

                    // Задержка между запросами, чтобы не превысить rate limit (SeatGeek может блокировать при слишком частых запросах)
                    // Задержка между запросами, чтобы не превысить rate limit и не вызвать блокировку Varnish
                    // Увеличиваем задержку для первых запросов
                    var delay = citiesProcessed <= 3 ? 1000 : 500;
                    await Task.Delay(delay);
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
                _logger?.LogWarning("⚠️ Не удалось получить события из SeatGeek API.");
                _logger?.LogInformation("ℹ️ Продолжаем работу без событий из SeatGeek");
                return;
            }

            _logger?.LogInformation("🎯 Всего получено {Count} событий из SeatGeek API", allEvents.Count);

            // Фильтруем только развлекательные события
            var eventsToProcess = new List<SeatGeekEvent>();
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
                        _logger?.LogDebug("❌ Отфильтровано событие: {Title}", evt.Title);
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
                if (string.IsNullOrWhiteSpace(evt.Title))
                {
                    continue;
                }

                var placeName = evt.Title.Trim();
                if (placeName.Length > 200)
                {
                    placeName = placeName.Substring(0, 200);
                }

                // Переводим название на русский
                var translatedName = placeName;
                if (_translationService != null)
                {
                    try
                    {
                        translatedName = await _translationService.TranslateToRussianAsync(placeName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Не удалось перевести название: {Name}", placeName);
                    }
                }

                // Проверяем, существует ли уже место с таким названием
                var existingPlace = await _context.Places
                    .FirstOrDefaultAsync(p => p.Name == translatedName);

                if (existingPlace != null)
                {
                    // Обновляем существующее место
                    existingPlace.Description = await TranslateDescriptionAsync(evt.Description ?? evt.ShortTitle ?? "");
                    existingPlace.Address = evt.Venue?.Address ?? evt.Venue?.Name ?? "";
                    existingPlace.Latitude = evt.Venue?.Location?.Lat;
                    existingPlace.Longitude = evt.Venue?.Location?.Lon;
                    existingPlace.ImageUrl = evt.Performers?.FirstOrDefault()?.Image ?? evt.Image;
                    existingPlace.UpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }
                else
                {
                    // Создаем новое место
                    var place = new Place
                    {
                        Name = translatedName,
                        Description = await TranslateDescriptionAsync(evt.Description ?? evt.ShortTitle ?? ""),
                        Address = evt.Venue?.Address ?? evt.Venue?.Name ?? "",
                        Latitude = evt.Venue?.Location?.Lat,
                        Longitude = evt.Venue?.Location?.Lon,
                        ImageUrl = evt.Performers?.FirstOrDefault()?.Image ?? evt.Image,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Places.Add(place);
                    addedCount++;

                    // Определяем категории для места
                    var categories = DetermineCategoriesFromEvent(evt);
                    foreach (var categoryName in categories)
                    {
                        var category = await _context.Categories
                            .FirstOrDefaultAsync(c => c.Name == categoryName);
                        if (category != null)
                        {
                            place.PlaceCategories.Add(new PlaceCategory
                            {
                                Place = place,
                                Category = category
                            });
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            var totalAddedCount = addedCount + updatedCount;
            if (totalAddedCount == 0)
            {
                _logger?.LogWarning("⚠️ Не было добавлено или обновлено ни одного места из SeatGeek");
            }
            else
            {
                _logger?.LogInformation("🎉 УСПЕХ: Добавлено {Added} новых мест, обновлено {Updated} мест в БД из SeatGeek!", 
                    addedCount, updatedCount);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке мест из SeatGeek");
            throw new Exception($"Ошибка при загрузке мест из SeatGeek: {ex.Message}", ex);
        }
    }

    private bool IsEntertainmentEvent(SeatGeekEvent evt)
    {
        if (evt == null) return false;

        var title = (evt.Title ?? "").ToLower();
        var description = (evt.Description ?? evt.ShortTitle ?? "").ToLower();
        var type = (evt.Type ?? "").ToLower();
        var genre = (evt.Genres?.FirstOrDefault()?.Name ?? "").ToLower();

        // Ключевые слова для развлекательных событий
        var entertainmentKeywords = new[]
        {
            "concert", "music", "festival", "show", "performance", "theater", "theatre",
            "comedy", "dance", "sport", "game", "match", "tournament", "exhibition",
            "museum", "art", "gallery", "cinema", "film", "movie", "party", "event",
            "entertainment", "amusement", "park", "attraction", "tour", "excursion",
            "концерт", "музыка", "фестиваль", "шоу", "спектакль", "театр", "комедия",
            "танец", "спорт", "игра", "матч", "турнир", "выставка", "музей", "искусство",
            "галерея", "кино", "фильм", "вечеринка", "событие", "развлечение", "парк",
            "аттракцион", "тур", "экскурсия"
        };

        return entertainmentKeywords.Any(keyword => 
            title.Contains(keyword) || description.Contains(keyword) || type.Contains(keyword) || genre.Contains(keyword));
    }

    private List<string> DetermineCategoriesFromEvent(SeatGeekEvent evt)
    {
        var categories = new List<string>();
        if (evt == null) return categories;

        var title = (evt.Title ?? "").ToLower();
        var description = (evt.Description ?? evt.ShortTitle ?? "").ToLower();
        var type = (evt.Type ?? "").ToLower();

        // Определяем категории на основе типа события
        if (type.Contains("concert") || type.Contains("music") || title.Contains("concert") || title.Contains("music"))
        {
            categories.Add("С друзьями");
        }

        if (type.Contains("sport") || type.Contains("game") || title.Contains("sport") || title.Contains("game"))
        {
            categories.Add("Активный отдых");
        }

        if (type.Contains("theater") || type.Contains("theatre") || type.Contains("show"))
        {
            categories.Add("Спокойный отдых");
        }

        if (type.Contains("museum") || type.Contains("exhibition") || type.Contains("gallery"))
        {
            categories.Add("С семьей");
            categories.Add("С ребенком");
        }

        // Если категории не определены, добавляем общие
        if (!categories.Any())
        {
            categories.Add("С друзьями");
        }

        return categories;
    }

    private async Task<string> TranslateDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "";

        if (_translationService != null)
        {
            try
            {
                return await _translationService.TranslateToRussianAsync(description);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Не удалось перевести описание");
            }
        }

        return description;
    }
}

// Модели для SeatGeek API
public class SeatGeekResponse
{
    public List<SeatGeekEvent>? Events { get; set; }
    public SeatGeekMeta? Meta { get; set; }
}

public class SeatGeekMeta
{
    public int? Total { get; set; }
    public int? PerPage { get; set; }
    public int? Page { get; set; }
    public bool? HasMore { get; set; }
}

public class SeatGeekEvent
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? ShortTitle { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Image { get; set; }
    public SeatGeekVenue? Venue { get; set; }
    public List<SeatGeekPerformer>? Performers { get; set; }
    public List<SeatGeekGenre>? Genres { get; set; }
    public SeatGeekDateTime? DatetimeLocal { get; set; }
}

public class SeatGeekVenue
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public SeatGeekLocation? Location { get; set; }
}

public class SeatGeekLocation
{
    public decimal? Lat { get; set; }
    public decimal? Lon { get; set; }
}

public class SeatGeekPerformer
{
    public string? Name { get; set; }
    public string? Image { get; set; }
}

public class SeatGeekGenre
{
    public string? Name { get; set; }
}

public class SeatGeekDateTime
{
    public DateTime? DateTime { get; set; }
}

