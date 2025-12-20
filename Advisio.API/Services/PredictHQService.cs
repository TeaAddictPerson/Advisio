using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public interface IPredictHQService
{
    Task LoadPlacesFromPredictHQAsync();
}

public class PredictHQService : IPredictHQService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PredictHQService>? _logger;
    private readonly ITranslationService? _translationService;

    public PredictHQService(
        AdvisioDbContext context,
        IPlaceService placeService,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PredictHQService>? logger = null,
        ITranslationService? translationService = null)
    {
        _context = context;
        _placeService = placeService;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _translationService = translationService;
    }

    public async Task LoadPlacesFromPredictHQAsync()
    {
        try
        {
            var apiToken = _configuration["PredictHQ:ApiToken"];
            if (string.IsNullOrEmpty(apiToken))
            {
                _logger?.LogError("PredictHQ API токен не настроен. Установите PredictHQ:ApiToken в appsettings.json");
                throw new Exception("PredictHQ API токен не настроен");
            }

            // Очищаем заголовки и настраиваем базовый адрес
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.BaseAddress = new Uri("https://api.predicthq.com/v1/");
            
            _logger?.LogInformation("✅ PredictHQ API настроен. Токен: {Token}", 
                string.IsNullOrEmpty(apiToken) ? "НЕ НАСТРОЕН" : $"{apiToken.Substring(0, Math.Min(10, apiToken.Length))}...");
            
            // Функция для создания запроса с правильными заголовками
            async Task<HttpResponseMessage> MakeRequestAsync(string url)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "Advisio/1.0");
                
                _logger?.LogDebug("📤 Отправка запроса: {Url}", url);
                _logger?.LogDebug("📤 Authorization: Bearer {Token}", apiToken.Substring(0, Math.Min(10, apiToken.Length)) + "...");
                
                return await _httpClient.SendAsync(request);
            }
            
            // Тестовый запрос для проверки аутентификации
            try
            {
                _logger?.LogInformation("🔍 Тестовый запрос к PredictHQ API для проверки аутентификации...");
                var testUrl = "events/?limit=1";
                var testResponse = await MakeRequestAsync(testUrl);
                var testContent = await testResponse.Content.ReadAsStringAsync();
                
                _logger?.LogInformation("📊 Тестовый ответ: Status={Status}, ContentLength={Length}", 
                    testResponse.StatusCode, testContent.Length);
                
                if (!testResponse.IsSuccessStatusCode)
                {
                    _logger?.LogError("❌ ОШИБКА АУТЕНТИФИКАЦИИ: Status={Status}, Response={Response}", 
                        testResponse.StatusCode, testContent.Substring(0, Math.Min(500, testContent.Length)));
                    
                    // Проверяем, может быть токен нужно передавать по-другому
                    if (testResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger?.LogError("⚠️ Токен неверный или истек. Проверьте токен в appsettings.json");
                        _logger?.LogError("⚠️ Убедитесь, что токен активен в панели PredictHQ");
                        _logger?.LogError("⚠️ Инструкции:");
                        _logger?.LogError("   1. Зайдите на https://predicthq.com/ и войдите в аккаунт");
                        _logger?.LogError("   2. Перейдите в раздел API Settings / Access Tokens");
                        _logger?.LogError("   3. Создайте новый токен или проверьте статус существующего");
                        _logger?.LogError("   4. Убедитесь, что токен имеет права на Events API");
                        _logger?.LogError("   5. Обновите токен в appsettings.json");
                        throw new Exception($"Ошибка аутентификации PredictHQ API. Токен неверный или истек. Проверьте токен в панели PredictHQ: {testContent}");
                    }
                }
                else
                {
                    _logger?.LogInformation("✅ Аутентификация успешна!");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Ошибка при тестовом запросе к PredictHQ API");
                throw;
            }

            _logger?.LogInformation("🔍 Ищем публичные события в PredictHQ API по координатам...");

            // Список крупных городов мира с координатами и радиусом (в км для PredictHQ)
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
                ("Moscow, Russia", 55.7558, 37.6173, 50),
                ("Saint Petersburg, Russia", 59.9343, 30.3351, 50),
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
                ("New York, USA", 40.7128, -74.0060, 50),
                ("Los Angeles, USA", 34.0522, -118.2437, 50),
                ("Chicago, USA", 41.8781, -87.6298, 50),
                ("Houston, USA", 29.7604, -95.3698, 50),
                ("Phoenix, USA", 33.4484, -112.0740, 50),
                ("Philadelphia, USA", 39.9526, -75.1652, 50),
                ("San Antonio, USA", 29.4241, -98.4936, 50),
                ("San Diego, USA", 32.7157, -117.1611, 50),
                ("Dallas, USA", 32.7767, -96.7970, 50),
                ("San Jose, USA", 37.3382, -121.8863, 50),
                // Канада
                ("Toronto, Canada", 43.6532, -79.3832, 30),
                ("Vancouver, Canada", 49.2827, -123.1207, 30),
                ("Montreal, Canada", 45.5017, -73.5673, 30),
                ("Calgary, Canada", 51.0447, -114.0719, 30),
                ("Ottawa, Canada", 45.4215, -75.6972, 30),
                // Австралия
                ("Sydney, Australia", -33.8688, 151.2093, 30),
                ("Melbourne, Australia", -37.8136, 144.9631, 30),
                ("Brisbane, Australia", -27.4698, 153.0251, 30),
                ("Perth, Australia", -31.9505, 115.8605, 30),
                ("Adelaide, Australia", -34.9285, 138.6007, 30),
                // Азия
                ("Tokyo, Japan", 35.6762, 139.6503, 50),
                ("Seoul, South Korea", 37.5665, 126.9780, 50),
                ("Singapore", 1.3521, 103.8198, 30),
                ("Hong Kong", 22.3193, 114.1694, 30),
                ("Bangkok, Thailand", 13.7563, 100.5018, 30),
                ("Kuala Lumpur, Malaysia", 3.1390, 101.6869, 30),
                ("Jakarta, Indonesia", -6.2088, 106.8456, 30),
                ("Manila, Philippines", 14.5995, 120.9842, 30),
                ("Mumbai, India", 19.0760, 72.8777, 50),
                ("Delhi, India", 28.6139, 77.2090, 50),
                ("Bangalore, India", 12.9716, 77.5946, 30),
                ("Shanghai, China", 31.2304, 121.4737, 50),
                ("Beijing, China", 39.9042, 116.4074, 50),
                ("Shenzhen, China", 22.5431, 114.0579, 30),
                // Ближний Восток
                ("Dubai, UAE", 25.2048, 55.2708, 30),
                ("Abu Dhabi, UAE", 24.4539, 54.3773, 30),
                ("Riyadh, Saudi Arabia", 24.7136, 46.6753, 30),
                ("Tel Aviv, Israel", 32.0853, 34.7818, 30),
                ("Istanbul, Turkey", 41.0082, 28.9784, 50),
                // Южная Америка
                ("São Paulo, Brazil", -23.5505, -46.6333, 50),
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

            var maxPlaces = _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 200);
            var maxCities = 10; // Ограничиваем количество городов для ускорения
            _logger?.LogInformation("📌 Максимальное количество событий: {MaxPlaces}, максимум городов: {MaxCities}", maxPlaces, maxCities);
            
            var allEvents = new List<PredictHQEvent>();
            var citiesProcessed = 0;
            var citiesWithEvents = 0;

            foreach (var (cityName, lat, lon, radiusKm) in cities.Take(maxCities))
            {
                // Проверяем лимит перед началом обработки города
                if (allEvents.Count >= maxPlaces)
                {
                    _logger?.LogInformation("✅ Достигнут лимит событий ({MaxPlaces}), останавливаем поиск", maxPlaces);
                    break;
                }

                citiesProcessed++;

                try
                {
                    // PredictHQ API: https://api.predicthq.com/v1/events?location_around.origin=lat,lon&location_around.radius=radiuskm
                    var queryParams = new Dictionary<string, string>
                    {
                        { "location_around.origin", $"{lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" },
                        { "location_around.radius", $"{radiusKm}km" },
                        { "limit", "50" },
                        { "offset", "0" },
                        { "category", "concerts,conferences,expos,festivals,performing-arts,sports" } // Только развлекательные события
                    };

                    var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                    var url = $"events/?{queryString}";

                    if (citiesProcessed % 10 == 0 || citiesProcessed <= 5)
                    {
                        _logger?.LogInformation("🔍 [{Processed}/{Total}] Ищем события в городе: {City} ({Lat}, {Lon})", 
                            citiesProcessed, cities.Length, cityName, lat, lon);
                    }

                    HttpResponseMessage? httpResponse = null;
                    string response = "";

                    try
                    {
                        // Используем HttpRequestMessage для правильной установки заголовков
                        using var request = new HttpRequestMessage(HttpMethod.Get, url);
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("User-Agent", "Advisio/1.0");
                        
                        // Добавляем таймаут для предотвращения зависания
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        try
                        {
                            httpResponse = await _httpClient.SendAsync(request, cts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            _logger?.LogWarning("⏱️ Таймаут запроса к PredictHQ API для города {City}", cityName);
                            continue;
                        }
                        response = await httpResponse.Content.ReadAsStringAsync();

                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            _logger?.LogWarning("⚠️ PredictHQ API вернул {StatusCode} для города {City}: {Response}", 
                                httpResponse.StatusCode, cityName, response.Substring(0, Math.Min(200, response.Length)));
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "❌ Ошибка HTTP запроса к PredictHQ API для города {City}", cityName);
                        continue;
                    }

                    PredictHQResponse? data = null;
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        options.Converters.Add(new PredictHQLocationConverter());
                        
                        data = JsonSerializer.Deserialize<PredictHQResponse>(response, options);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "❌ Ошибка десериализации ответа PredictHQ API для города {City}: {Error}", cityName, ex.Message);
                        _logger?.LogDebug("📄 Первые 500 символов ответа: {Response}", response.Substring(0, Math.Min(500, response.Length)));
                        continue;
                    }

                    if (data?.Results != null && data.Results.Any())
                    {
                        citiesWithEvents++;
                        var totalInCity = data.Results.Count;
                        var newEvents = data.Results.Where(e => !allEvents.Any(existing => 
                            existing.Id == e.Id)).ToList();

                        _logger?.LogInformation("📊 Город {City}: получено {Total} событий, из них {New} новых", 
                            cityName, totalInCity, newEvents.Count);

                        // Ограничиваем количество новых событий, чтобы не превысить лимит
                        var remainingSlots = maxPlaces - allEvents.Count;
                        if (remainingSlots > 0)
                        {
                            var eventsToAdd = newEvents.Take(remainingSlots).ToList();
                            allEvents.AddRange(eventsToAdd);

                            if (citiesProcessed % 10 == 0 || citiesProcessed <= 5 || eventsToAdd.Count > 0)
                            {
                                _logger?.LogInformation("✅ Город {City}: добавлено {NewCount} новых событий (всего: {Total}/{Max})", 
                                    cityName, eventsToAdd.Count, allEvents.Count, maxPlaces);
                            }
                            
                            // Логируем примеры событий для первых городов
                            if (citiesProcessed <= 3 && eventsToAdd.Any())
                            {
                                foreach (var sample in eventsToAdd.Take(2))
                                {
                                    _logger?.LogInformation("  📌 Событие: {Title} (ID: {Id})", 
                                        sample.Title ?? "Без названия", sample.Id ?? "нет ID");
                                }
                            }

                            // Если достигли лимита, прекращаем пагинацию
                            if (allEvents.Count >= maxPlaces)
                            {
                                _logger?.LogInformation("✅ Достигнут лимит событий ({MaxPlaces}), пропускаем пагинацию для города {City}", 
                                    maxPlaces, cityName);
                                continue;
                            }
                        }
                        else
                        {
                            _logger?.LogInformation("⏸️ Лимит событий достигнут, пропускаем город {City}", cityName);
                            continue;
                        }

                        // Пагинация - только если не достигнут лимит
                        // Ограничиваем пагинацию: максимум 2 страницы на город (100 событий)
                        var totalCount = data.Count ?? 0;
                        var limit = 50;
                        var offset = limit;
                        var maxPages = 2; // Максимум 2 страницы на город (100 событий)

                        while (offset < totalCount && allEvents.Count < maxPlaces && (offset / limit) < maxPages)
                        {
                            try
                            {
                                var pageParams = new Dictionary<string, string>
                                {
                                    { "location_around.origin", $"{lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" },
                                    { "location_around.radius", $"{radiusKm}km" },
                                    { "limit", limit.ToString() },
                                    { "offset", offset.ToString() },
                                    { "category", "concerts,conferences,expos,festivals,performing-arts,sports" }
                                };

                                var pageQueryString = string.Join("&", pageParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                                var pageUrl = $"events/?{pageQueryString}";

                                // Проверяем лимит перед каждым запросом
                                if (allEvents.Count >= maxPlaces)
                                {
                                    _logger?.LogInformation("✅ Достигнут лимит событий ({MaxPlaces}) перед запросом страницы", maxPlaces);
                                    break;
                                }

                                // Используем HttpRequestMessage для правильной установки заголовков
                                using var pageRequest = new HttpRequestMessage(HttpMethod.Get, pageUrl);
                                pageRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
                                pageRequest.Headers.Add("Accept", "application/json");
                                pageRequest.Headers.Add("User-Agent", "Advisio/1.0");
                                
                                // Добавляем таймаут для предотвращения зависания
                                using var pageCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                                HttpResponseMessage pageResponse;
                                try
                                {
                                    pageResponse = await _httpClient.SendAsync(pageRequest, pageCts.Token);
                                }
                                catch (TaskCanceledException)
                                {
                                    _logger?.LogWarning("⏱️ Таймаут запроса страницы для города {City}", cityName);
                                    break;
                                }
                                var pageContent = await pageResponse.Content.ReadAsStringAsync();

                                if (pageResponse.IsSuccessStatusCode)
                                {
                                    var pageOptions = new JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    };
                                    pageOptions.Converters.Add(new PredictHQLocationConverter());
                                    
                                    var pageData = JsonSerializer.Deserialize<PredictHQResponse>(pageContent, pageOptions);

                                    if (pageData?.Results != null && pageData.Results.Any())
                                    {
                                        var pageNewEvents = pageData.Results.Where(e => !allEvents.Any(existing => 
                                            existing.Id == e.Id)).ToList();
                                        
                                        // Ограничиваем количество событий, чтобы не превысить лимит
                                        var remainingSlotsPage = maxPlaces - allEvents.Count;
                                        if (remainingSlotsPage > 0)
                                        {
                                            var eventsToAdd = pageNewEvents.Take(remainingSlotsPage).ToList();
                                            allEvents.AddRange(eventsToAdd);

                                            if (allEvents.Count >= maxPlaces)
                                            {
                                                _logger?.LogInformation("✅ Достигнут лимит событий ({MaxPlaces}) при пагинации города {City}", 
                                                    maxPlaces, cityName);
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    break;
                                }

                                offset += limit;
                                
                                // Проверяем лимит перед следующей страницей
                                if (allEvents.Count >= maxPlaces)
                                {
                                    break;
                                }
                                
                                await Task.Delay(200); // Уменьшенная задержка между страницами
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "❌ Ошибка при загрузке страницы для города {City}", cityName);
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (citiesProcessed <= 5)
                        {
                            _logger?.LogInformation("ℹ️ Город {City}: события не найдены (Results={Results}, Count={Count})", 
                                cityName, 
                                data?.Results != null ? "не null" : "null",
                                data?.Count ?? 0);
                        }
                    }

                    // Проверяем лимит после обработки города
                    if (allEvents.Count >= maxPlaces)
                    {
                        _logger?.LogInformation("✅ Достигнут лимит событий ({MaxPlaces}) после обработки города {City}", 
                            maxPlaces, cityName);
                        break;
                    }

                    // Задержка между запросами (уменьшена для ускорения)
                    var delay = citiesProcessed <= 3 ? 500 : 200;
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "❌ Ошибка при поиске событий в городе {City}", cityName);
                }
            }

            _logger?.LogInformation("📊 Обработано городов: {Processed}/{Total}, городов с событиями: {WithEvents}, всего событий: {TotalEvents}", 
                citiesProcessed, cities.Length, citiesWithEvents, allEvents.Count);

            if (allEvents.Count == 0)
            {
                _logger?.LogWarning("⚠️ Не найдено событий для сохранения");
                
                // Проверяем текущее количество мест в БД для диагностики
                var currentPlacesCount = await _context.Places.CountAsync();
                _logger?.LogInformation("📊 Текущее количество мест в базе данных: {Count}", currentPlacesCount);
                
                return;
            }

            _logger?.LogInformation("💾 Начинаем сохранение {Count} событий в базу данных...", allEvents.Count);

            // Логируем первые несколько событий для диагностики
            if (allEvents.Count > 0)
            {
                _logger?.LogInformation("📋 Примеры событий для сохранения:");
                foreach (var sampleEvent in allEvents.Take(3))
                {
                    _logger?.LogInformation("  - ID: {Id}, Title: {Title}, Location: {Location}", 
                        sampleEvent.Id, 
                        sampleEvent.Title ?? "null", 
                        sampleEvent.Location?.Name ?? "null");
                }
            }

            // Сохраняем события в базу данных
            var addedCount = 0;
            var updatedCount = 0;
            var errorCount = 0;
            var skippedCount = 0;

            // Сохраняем события батчами для ускорения
            const int batchSize = 50; // Уменьшили размер батча для более частого сохранения
            var processedCount = 0;
            
            foreach (var phqEvent in allEvents)
            {
                try
                {
                    // Проверяем обязательные поля
                    if (string.IsNullOrWhiteSpace(phqEvent.Title))
                    {
                        _logger?.LogWarning("⚠️ Пропущено событие без названия: ID={Id}", phqEvent.Id);
                        skippedCount++;
                        continue;
                    }

                    // Проверяем, существует ли уже место с таким названием или координатами
                    Place? existingPlace = null;
                    
                    // Сначала проверяем по точному совпадению названия
                    if (!string.IsNullOrWhiteSpace(phqEvent.Title))
                    {
                        existingPlace = await _context.Places
                            .FirstOrDefaultAsync(p => p.Name.Trim().ToLower() == phqEvent.Title.Trim().ToLower());
                    }
                    
                    // Если не найдено по названию, проверяем по координатам (если они есть)
                    if (existingPlace == null && phqEvent.Location?.Lat.HasValue == true && phqEvent.Location.Lon.HasValue == true)
                    {
                        var lat = (decimal)phqEvent.Location.Lat.Value;
                        var lon = (decimal)phqEvent.Location.Lon.Value;
                        
                        // Ищем места в радиусе ~100 метров (примерно 0.001 градуса)
                        var tolerance = 0.001m;
                        existingPlace = await _context.Places
                            .FirstOrDefaultAsync(p => 
                                p.Latitude.HasValue && p.Longitude.HasValue &&
                                Math.Abs(p.Latitude.Value - lat) < tolerance &&
                                Math.Abs(p.Longitude.Value - lon) < tolerance);
                    }

                    if (existingPlace != null)
                    {
                        // Обновляем существующее место
                        _logger?.LogDebug("🔄 Обновление существующего места: ID={PlaceId}, Name={Name}", 
                            existingPlace.Id, existingPlace.Name);
                        
                        existingPlace.Description = phqEvent.Description ?? existingPlace.Description;
                        if (phqEvent.Location != null)
                        {
                            existingPlace.Address = phqEvent.Location.Name ?? existingPlace.Address;
                            if (phqEvent.Location.Lat.HasValue && phqEvent.Location.Lon.HasValue)
                            {
                                existingPlace.Latitude = (decimal)phqEvent.Location.Lat.Value;
                                existingPlace.Longitude = (decimal)phqEvent.Location.Lon.Value;
                            }
                        }
                        if (!string.IsNullOrEmpty(phqEvent.Thumbnail))
                        {
                            existingPlace.ImageUrl = phqEvent.Thumbnail;
                        }
                        existingPlace.UpdatedAt = DateTime.UtcNow;
                        updatedCount++;
                    }
                    else
                    {
                        // Создаем новое место
                        var place = new Place
                        {
                            Name = phqEvent.Title.Trim(),
                            Description = phqEvent.Description ?? "",
                            Address = phqEvent.Location?.Name ?? "",
                            Latitude = phqEvent.Location?.Lat.HasValue == true ? (decimal)phqEvent.Location.Lat.Value : null,
                            Longitude = phqEvent.Location?.Lon.HasValue == true ? (decimal)phqEvent.Location.Lon.Value : null,
                            ImageUrl = phqEvent.Thumbnail ?? "",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        // Переводим название и описание на русский, если доступен сервис перевода
                        if (_translationService != null)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(place.Name))
                                {
                                    place.Name = await _translationService.TranslateToRussianAsync(place.Name);
                                }
                                if (!string.IsNullOrEmpty(place.Description))
                                {
                                    place.Description = await _translationService.TranslateToRussianAsync(place.Description);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "⚠️ Ошибка при переводе текста для события {EventId}: {Error}", phqEvent.Id, ex.Message);
                            }
                        }

                        _context.Places.Add(place);
                        addedCount++;
                        
                        if (addedCount <= 5)
                        {
                            _logger?.LogInformation("➕ Добавлено новое место: {Name} (ID события: {EventId})", 
                                place.Name, phqEvent.Id);
                        }
                    }
                    
                    processedCount++;
                    
                    // Сохраняем батчами для ускорения
                    if (processedCount % batchSize == 0)
                    {
                        _logger?.LogInformation("💾 Сохраняем батч {Processed}/{Total} (добавлено: {Added}, обновлено: {Updated})...", 
                            processedCount, allEvents.Count, addedCount, updatedCount);
                        try
                        {
                            var saved = await _context.SaveChangesAsync();
                            _logger?.LogInformation("✅ Батч сохранен: {Saved} записей", saved);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "❌ Ошибка при сохранении батча: {Error}", ex.Message);
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger?.LogWarning(ex, "❌ Ошибка при сохранении события {EventId}: {Error}", phqEvent.Id, ex.Message);
                    if (errorCount <= 5)
                    {
                        _logger?.LogDebug("Детали ошибки для события {EventId}: Title={Title}, Location={Location}, Lat={Lat}, Lon={Lon}", 
                            phqEvent.Id, 
                            phqEvent.Title, 
                            phqEvent.Location?.Name ?? "null",
                            phqEvent.Location?.Lat ?? 0,
                            phqEvent.Location?.Lon ?? 0);
                    }
                }
            }

            _logger?.LogInformation("💾 Сохраняем оставшиеся изменения в базу данных...");
            
            try
            {
                var finalSaved = await _context.SaveChangesAsync();
                _logger?.LogInformation("✅ Финальное сохранение завершено: {Saved} записей", finalSaved);
                
                // Проверяем, что места действительно сохранились в БД
                var totalPlacesInDb = await _context.Places.CountAsync();
                _logger?.LogInformation("📊 Всего мест в базе данных после синхронизации: {TotalPlaces}", totalPlacesInDb);
                
                // Проверяем последние добавленные места
                var recentPlaces = await _context.Places
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .Select(p => new { p.Id, p.Name, p.CreatedAt })
                    .ToListAsync();
                
                if (recentPlaces.Any())
                {
                    _logger?.LogInformation("📋 Последние добавленные места:");
                    foreach (var place in recentPlaces)
                    {
                        _logger?.LogInformation("  - ID: {Id}, Name: {Name}, CreatedAt: {CreatedAt}", 
                            place.Id, place.Name, place.CreatedAt);
                    }
                }
                else
                {
                    _logger?.LogWarning("⚠️ В базе данных нет мест после синхронизации!");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ КРИТИЧЕСКАЯ ОШИБКА при сохранении в базу данных: {Error}", ex.Message);
                _logger?.LogError("Детали ошибки: {Details}", ex.ToString());
                throw;
            }

            _logger?.LogInformation("✅ Загружено из PredictHQ: добавлено {Added}, обновлено {Updated}, пропущено {Skipped}, ошибок {Errors}, всего событий обработано: {Total}", 
                addedCount, updatedCount, skippedCount, errorCount, allEvents.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Критическая ошибка при загрузке мест из PredictHQ");
            throw;
        }
    }
}

// Модели для PredictHQ API
public class PredictHQResponse
{
    public List<PredictHQEvent>? Results { get; set; }
    public int? Count { get; set; }
    public string? Next { get; set; }
    public string? Previous { get; set; }
}

public class PredictHQEvent
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public PredictHQLocation? Location { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? Thumbnail { get; set; }
    public int? Rank { get; set; }
}

public class PredictHQLocation
{
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
}

// Кастомный конвертер для обработки разных форматов location в PredictHQ API
public class PredictHQLocationConverter : JsonConverter<PredictHQLocation>
{
    public override PredictHQLocation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var location = new PredictHQLocation();

        // Если location - это массив [longitude, latitude] (GeoJSON формат)
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            if (reader.TokenType == JsonTokenType.Number)
            {
                var lon = reader.GetDouble();
                reader.Read();
                if (reader.TokenType == JsonTokenType.Number)
                {
                    var lat = reader.GetDouble();
                    location.Lon = lon;
                    location.Lat = lat;
                    reader.Read(); // Читаем EndArray
                    return location;
                }
            }
        }

        // Если location - это объект
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();

                    switch (propertyName?.ToLowerInvariant())
                    {
                        case "lat":
                        case "latitude":
                            if (reader.TokenType == JsonTokenType.Number)
                            {
                                location.Lat = reader.GetDouble();
                            }
                            break;
                        case "lon":
                        case "lng":
                        case "longitude":
                            if (reader.TokenType == JsonTokenType.Number)
                            {
                                location.Lon = reader.GetDouble();
                            }
                            break;
                        case "name":
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                location.Name = reader.GetString();
                            }
                            break;
                        case "address":
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                location.Address = reader.GetString();
                            }
                            break;
                        default:
                            // Пропускаем неизвестные свойства
                            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                            {
                                reader.Skip();
                            }
                            break;
                    }
                }
            }
        }

        return location;
    }

    public override void Write(Utf8JsonWriter writer, PredictHQLocation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Lat.HasValue)
        {
            writer.WriteNumber("lat", value.Lat.Value);
        }
        if (value.Lon.HasValue)
        {
            writer.WriteNumber("lon", value.Lon.Value);
        }
        if (!string.IsNullOrEmpty(value.Name))
        {
            writer.WriteString("name", value.Name);
        }
        if (!string.IsNullOrEmpty(value.Address))
        {
            writer.WriteString("address", value.Address);
        }
        writer.WriteEndObject();
    }
}

