using System.Text.Json;
using System.Net.Http;
using System.Linq;
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
            // Пробуем разные endpoint'ы Kufar API
            // Также пробуем неофициальные endpoint'ы, которые могут работать после получения cookies
            var apiUrls = new[]
            {
                "https://api.kufar.by/search-api/v1/search/rendered-paginated",
                "https://api.kufar.by/search-api/v1/search/rendered",
                "https://api.kufar.by/search-api/v1/search",
                "https://api.kufar.by/v1/search",
                "https://www.kufar.by/api/search-api/v1/search/rendered-paginated", // Альтернативный endpoint
                "https://www.kufar.by/api/v1/search" // Неофициальный endpoint
            };
            
            var apiUrl = apiUrls[0]; // Используем первый URL
            
            // Сначала пробуем получить cookies с главной страницы
            try
            {
                _logger?.LogInformation("Получение cookies с главной страницы Kufar...");
                var tempHandler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    UseCookies = true,
                    CookieContainer = new System.Net.CookieContainer()
                };
                
                using var tempClient = new HttpClient(tempHandler)
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
                
                tempClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                tempClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                tempClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                
                var homeResponse = await tempClient.GetAsync("https://www.kufar.by/");
                homeResponse.EnsureSuccessStatusCode();
                
                // Копируем cookies в основной клиент
                var cookies = tempHandler.CookieContainer.GetCookies(new Uri("https://www.kufar.by"));
                foreach (System.Net.Cookie cookie in cookies)
                {
                    _httpClient.DefaultRequestHeaders.Add("Cookie", $"{cookie.Name}={cookie.Value}");
                }
                
                _logger?.LogInformation("Cookies получены успешно");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Не удалось получить cookies с главной страницы, продолжаем без них");
            }
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.kufar.by/");

            var categories = await _context.Categories.ToListAsync();
            int totalAddedCount = 0;
            int totalUpdatedCount = 0;
            int maxPlaces = _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 1000);
            
            // Пробуем разные категории: развлечения, досуг, спорт, туризм
            // 1700-1709 - развлечения и досуг, но они могут быть пустыми
            // Попробуем также другие категории: 1000 (услуги), 2000 (недвижимость), 3000 (транспорт)
            // Но для мест лучше использовать: 1700 (развлечения), 1600 (спорт), 1500 (туризм)
            // Пробуем разные категории: развлечения, спорт, туризм
            // 1700 - развлечения и досуг (основная категория)
            // Попробуем также без указания категории или с другими параметрами
            // Пробуем разные варианты категорий и регионов
            // Сначала пробуем с регионом Минск (rgn=1), потом всю Беларусь (rgn=0)
            // Категории, связанные с отдыхом и развлечениями
            var kufarCategories = new[] { 
                "1700", // Развлечения и досуг
                "1701", // Билеты и мероприятия
                "1702", // Клубы и бары
                "1703", // Рестораны и кафе
                "1600", // Спорт и отдых
                "1500"  // Туризм и путешествия
            };
            
            var regions = new[] { "1", "0" }; // 1 = Минск, 0 = Вся Беларусь
            int placesPerCategory = maxPlaces / (kufarCategories.Length * regions.Length);

            foreach (var region in regions)
            {
                foreach (var kufarCat in kufarCategories)
                {
                    int addedCount = 0;
                    int updatedCount = 0;
                    int totalProcessed = 0;
                    int pageSize = 100;
                    int currentPage = 0;
                    bool hasMore = true;

                    var regionName = region == "1" ? "Минск" : (region == "0" ? "Вся Беларусь" : "Без региона");
                    _logger?.LogInformation("Загрузка мест из категории Kufar: {Category}, регион: {Region}", kufarCat, regionName);

                    while (hasMore && totalProcessed < placesPerCategory)
                    {
                        // Формируем параметры запроса
                        var queryParamsList = new List<string>
                        {
                            $"cat={kufarCat}",
                            $"size={pageSize}",
                            "sort=lst.d",
                            $"cur={currentPage * pageSize}",
                            $"rgn={region}", // Регион: 1 = Минск, 0 = Вся Беларусь
                            "lang=ru" // Добавляем параметр языка
                        };
                        
                        var queryParams = "?" + string.Join("&", queryParamsList);
                        var fullUrl = apiUrl + queryParams;
                    
                        _logger?.LogInformation("Загрузка страницы {Page} из Kufar API: {Url}", currentPage + 1, fullUrl);

                        try
                        {
                            var response = await _httpClient.GetStringAsync(fullUrl);
                            
                            // Если получили пустой ответ или пустой массив ads, пробуем разные варианты
                            if ((string.IsNullOrWhiteSpace(response) || response.Contains("\"ads\":[]")) && currentPage == 0)
                            {
                                // Вариант 1: Без параметра sort
                                var queryParamsNoSort = new List<string>
                                {
                                    $"cat={kufarCat}",
                                    $"size={pageSize}",
                                    $"cur={currentPage * pageSize}",
                                    $"rgn={region}",
                                    "lang=ru"
                                };
                                var queryParamsAlt = "?" + string.Join("&", queryParamsNoSort);
                                var fullUrlAlt = apiUrl + queryParamsAlt;
                                _logger?.LogInformation("Пробуем альтернативный URL без sort: {Url}", fullUrlAlt);
                                
                                try
                                {
                                    var responseAlt = await _httpClient.GetStringAsync(fullUrlAlt);
                                    if (!string.IsNullOrWhiteSpace(responseAlt) && responseAlt.Contains("\"ads\"") && !responseAlt.Contains("\"ads\":[]"))
                                    {
                                        response = responseAlt;
                                        fullUrl = fullUrlAlt;
                                        _logger?.LogInformation("✅ Альтернативный URL без sort сработал!");
                                    }
                                }
                                catch (Exception exAlt)
                                {
                                    _logger?.LogWarning(exAlt, "Альтернативный запрос без sort не удался");
                                }
                                
                                // Вариант 2: Без категории (все объявления в регионе)
                                if (string.IsNullOrWhiteSpace(response) || response.Contains("\"ads\":[]"))
                                {
                                    var queryParamsNoCat = new List<string>
                                    {
                                        $"size={pageSize}",
                                        $"cur={currentPage * pageSize}",
                                        $"rgn={region}",
                                        "lang=ru"
                                    };
                                    var queryParamsNoCatStr = "?" + string.Join("&", queryParamsNoCat);
                                    var fullUrlNoCat = apiUrl + queryParamsNoCatStr;
                                    _logger?.LogInformation("Пробуем запрос без категории (все объявления): {Url}", fullUrlNoCat);
                                    
                                    try
                                    {
                                        var responseNoCat = await _httpClient.GetStringAsync(fullUrlNoCat);
                                        if (!string.IsNullOrWhiteSpace(responseNoCat) && responseNoCat.Contains("\"ads\"") && !responseNoCat.Contains("\"ads\":[]"))
                                        {
                                            response = responseNoCat;
                                            fullUrl = fullUrlNoCat;
                                            _logger?.LogInformation("✅ Запрос без категории сработал!");
                                        }
                                    }
                                    catch (Exception exNoCat)
                                    {
                                        _logger?.LogWarning(exNoCat, "Запрос без категории не удался");
                                    }
                                }
                                
                                // Вариант 3: Попробуем другие endpoint'ы
                                if (string.IsNullOrWhiteSpace(response) || response.Contains("\"ads\":[]"))
                                {
                                    var alternativeEndpoints = new[]
                                    {
                                        "https://api.kufar.by/search-api/v1/search/rendered",
                                        "https://api.kufar.by/search-api/v1/search",
                                        "https://www.kufar.by/api/search-api/v1/search/rendered-paginated"
                                    };
                                    
                                    foreach (var altEndpoint in alternativeEndpoints)
                                    {
                                        var queryParamsAltEndpoint = "?" + string.Join("&", queryParamsList);
                                        var fullUrlAltEndpoint = altEndpoint + queryParamsAltEndpoint;
                                        _logger?.LogInformation("Пробуем альтернативный endpoint: {Url}", fullUrlAltEndpoint);
                                        
                                        try
                                        {
                                            var responseAltEndpoint = await _httpClient.GetStringAsync(fullUrlAltEndpoint);
                                            if (!string.IsNullOrWhiteSpace(responseAltEndpoint) && responseAltEndpoint.Contains("\"ads\"") && !responseAltEndpoint.Contains("\"ads\":[]"))
                                            {
                                                response = responseAltEndpoint;
                                                fullUrl = fullUrlAltEndpoint;
                                                _logger?.LogInformation("✅ Альтернативный endpoint {Endpoint} сработал!", altEndpoint);
                                                break;
                                            }
                                        }
                                        catch (Exception exAltEndpoint)
                                        {
                                            _logger?.LogWarning(exAltEndpoint, "Альтернативный endpoint {Endpoint} не удался", altEndpoint);
                                        }
                                    }
                                }
                            }
                            
                            if (string.IsNullOrWhiteSpace(response))
                            {
                                _logger?.LogWarning("Пустой ответ от Kufar API для страницы {Page}", currentPage + 1);
                                hasMore = false;
                                break;
                            }
                            
                            // Логируем полный ответ для первой страницы первой категории первого региона для анализа
                            if (currentPage == 0 && kufarCat == "1700" && (region == "1" || region == null))
                            {
                                _logger?.LogInformation("=== ПОЛНЫЙ ОТВЕТ KUFAR API (для анализа) ===");
                                _logger?.LogInformation("URL: {Url}", fullUrl);
                                _logger?.LogInformation("Ответ (первые 2000 символов): {Response}", 
                                    response.Length > 2000 ? response.Substring(0, 2000) : response);
                                if (response.Length > 2000)
                                {
                                    _logger?.LogInformation("... (всего {Length} символов)", response.Length);
                                }
                                _logger?.LogInformation("=== КОНЕЦ ОТВЕТА ===");
                            }
                            
                            var kufarData = JsonSerializer.Deserialize<KufarResponse>(response, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (kufarData == null)
                            {
                                _logger?.LogWarning("Не удалось десериализовать ответ от Kufar API для категории {Category}", kufarCat);
                                hasMore = false;
                                break;
                            }

                            if (kufarData.Ads == null || !kufarData.Ads.Any())
                            {
                                _logger?.LogInformation("Нет данных на странице {Page} категории {Category}, регион {Region}. Ads = {HasAds}, Total = {Total}", 
                                    currentPage + 1, kufarCat, regionName,
                                    kufarData.Ads != null ? "существует, но пусто" : "null",
                                    kufarData.Total ?? 0);
                                
                                // Если total > 0, но ads пусто, возможно нужна другая страница или параметры
                                if (kufarData.Total > 0)
                                {
                                    _logger?.LogWarning("⚠️ API вернул total={Total}, но ads пусто. Возможно, нужны другие параметры.", kufarData.Total);
                                }
                                
                                hasMore = false;
                                break;
                            }

                            var placesToProcess = kufarData.Ads.ToList();
                            _logger?.LogInformation("Обработка {Count} мест со страницы {Page}", placesToProcess.Count, currentPage + 1);

                    foreach (var ad in placesToProcess)
                    {
                        if (string.IsNullOrWhiteSpace(ad.Subject))
                        {
                            _logger?.LogDebug("Пропущено объявление без названия");
                            continue;
                        }

                        var placeName = ad.Subject.Trim();
                        if (placeName.Length > 200) // Ограничение длины имени
                        {
                            placeName = placeName.Substring(0, 200);
                        }

                        // Проверяем существующее место (case-insensitive)
                        var existingPlace = await _context.Places
                            .FirstOrDefaultAsync(p => p.Name != null && p.Name.ToLower() == placeName.ToLower());

                        if (existingPlace != null)
                        {
                            updatedCount++;
                            if (updatedCount % 50 == 0)
                            {
                                _logger?.LogInformation("⏭️ Пропущено (уже есть): {Count} мест", updatedCount);
                            }
                            totalProcessed++;
                            continue;
                        }

                        var createPlaceDto = new CreatePlaceDto
                        {
                            Name = placeName,
                            Description = (ad.Body ?? string.Empty).Length > 2000 ? (ad.Body ?? string.Empty).Substring(0, 2000) : (ad.Body ?? string.Empty),
                            Address = ad.Location?.Name?.Length > 200 ? ad.Location.Name.Substring(0, 200) : ad.Location?.Name,
                            ImageUrl = ad.Images?.FirstOrDefault()?.Url?.Length > 500 ? ad.Images.FirstOrDefault()?.Url?.Substring(0, 500) : ad.Images?.FirstOrDefault()?.Url,
                            Latitude = ad.Location?.Latitude,
                            Longitude = ad.Location?.Longitude,
                            CategoryIds = DetermineCategories(ad, categories)
                        };

                        // Валидация перед созданием
                        if (string.IsNullOrWhiteSpace(createPlaceDto.Name))
                        {
                            _logger?.LogWarning("Пропущено место с пустым именем после обработки");
                            totalProcessed++;
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
                            _logger?.LogError(ex, "❌ ОШИБКА добавления места: {Name} - {Error}", createPlaceDto.Name, ex.Message);
                            // Продолжаем обработку даже при ошибке
                        }
                        
                        totalProcessed++;
                    }

                    if (placesToProcess.Count < pageSize)
                    {
                        hasMore = false;
                    }
                    else
                    {
                        currentPage++;
                        await Task.Delay(1000);
                    }
                }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Ошибка при загрузке страницы {Page} категории {Category} из Kufar", currentPage + 1, kufarCat);
                        hasMore = false;
                    }
                }

                    totalUpdatedCount += updatedCount;
                    _logger?.LogInformation("📊 Категория {Category}, регион {Region}: ✅ Добавлено: {Added}, ⏭️ Пропущено: {Updated}, 📝 Обработано: {Total}", 
                        kufarCat, regionName, addedCount, updatedCount, totalProcessed);
                    
                    if (addedCount == 0 && totalProcessed > 0)
                    {
                        _logger?.LogWarning("⚠️ Категория {Category}, регион {Region}: Не добавлено ни одного нового места из {Processed} обработанных", 
                            kufarCat, regionName, totalProcessed);
                    }
                    
                    await Task.Delay(500);
                }
            }

            _logger?.LogInformation("🎯 KUFAR ИТОГО: ✅ ДОБАВЛЕНО: {Added} мест, ⏭️ ПРОПУЩЕНО: {Updated} мест", 
                totalAddedCount, totalUpdatedCount);
            
            if (totalAddedCount == 0)
            {
                _logger?.LogWarning("⚠️ Не добавлено ни одного нового места из Kufar. Возможно, все места уже есть в БД.");
            }
            else
            {
                _logger?.LogInformation("🎉 УСПЕХ: Добавлено {Count} новых мест в БД из Kufar!", totalAddedCount);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при загрузке мест из Kufar: {ex.Message}", ex);
        }
    }

    private async Task<string?> GetKvitkiAccessTokenAsync()
    {
        try
        {
            // Пробуем разные endpoint'ы для получения токена
            var tokenUrls = new[]
            {
                "https://www.kvitki.by/api/refresh",
                "https://www.kvitki.by/api/auth/refresh"
            };
            
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };
            
            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // Сначала получаем cookies с главной страницы
            try
            {
                _logger?.LogInformation("Получение cookies с главной страницы Kvitki для токена...");
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                
                var homeResponse = await httpClient.GetAsync("https://www.kvitki.by/");
                homeResponse.EnsureSuccessStatusCode();
                _logger?.LogInformation("Cookies с главной страницы Kvitki получены успешно");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Не удалось получить cookies с главной страницы Kvitki, продолжаем без них");
            }
            
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://www.kvitki.by/");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://www.kvitki.by");
            
            foreach (var tokenUrl in tokenUrls)
            {
                try
                {
                    _logger?.LogInformation("Попытка получения токена из: {Url}", tokenUrl);
                    
                    // Пробуем сначала GET запрос
                    var httpResponse = await httpClient.GetAsync(tokenUrl);
                    
                    // Если получили редирект, логируем его
                    if ((int)httpResponse.StatusCode >= 300 && (int)httpResponse.StatusCode < 400)
                    {
                        var location = httpResponse.Headers.Location?.ToString();
                        _logger?.LogInformation("Получен редирект {StatusCode} на: {Location}", httpResponse.StatusCode, location);
                        
                        // Пробуем следовать редиректу вручную
                        if (!string.IsNullOrEmpty(location))
                        {
                            if (!location.StartsWith("http"))
                            {
                                var baseUri = new Uri(tokenUrl);
                                location = new Uri(baseUri, location).ToString();
                            }
                            _logger?.LogInformation("Пробуем следовать редиректу на: {Location}", location);
                            httpResponse = await httpClient.GetAsync(location);
                        }
                    }
                    
                    httpResponse.EnsureSuccessStatusCode();
                    
                    var tokenResponse = await httpResponse.Content.ReadAsStringAsync();
                    
                    // Логируем ответ для анализа
                    _logger?.LogInformation("Ответ от refresh endpoint (первые 500 символов): {Response}", 
                        tokenResponse.Length > 500 ? tokenResponse.Substring(0, 500) : tokenResponse);
                    
                    // Проверяем, не является ли это ошибкой
                    var errorResponse = JsonSerializer.Deserialize<KvitkiResponse>(tokenResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (errorResponse != null && errorResponse.ResponseStatus == "fail")
                    {
                        _logger?.LogWarning("Endpoint вернул ошибку. Пробуем POST запрос...");
                        
                        // Пробуем POST запрос (возможно, нужен POST вместо GET)
                        var postContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PostAsync(tokenUrl, postContent);
                        httpResponse.EnsureSuccessStatusCode();
                        
                        tokenResponse = await httpResponse.Content.ReadAsStringAsync();
                        _logger?.LogInformation("Ответ от POST refresh endpoint (первые 500 символов): {Response}", 
                            tokenResponse.Length > 500 ? tokenResponse.Substring(0, 500) : tokenResponse);
                    }
                    
                    // Парсим ответ с токенами
                    var tokenData = JsonSerializer.Deserialize<KvitkiTokenResponse>(tokenResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
                    {
                        _logger?.LogInformation("✅ Токен успешно получен из: {Url}", tokenUrl);
                        return tokenData.AccessToken;
                    }
                    else
                    {
                        _logger?.LogWarning("Токен не найден в ответе от {Url}. Ответ: {Response}", tokenUrl, 
                            tokenResponse.Length > 200 ? tokenResponse.Substring(0, 200) : tokenResponse);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Не удалось получить токен из {Url}, пробуем следующий", tokenUrl);
                    continue;
                }
            }
            
            _logger?.LogWarning("Не удалось получить токен доступа для Kvitki API");
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при получении токена Kvitki");
            return null;
        }
    }

    public async Task LoadPlacesFromKvitkiAsync()
    {
        try
        {
            // Пробуем получить токен доступа (но не обязательно)
            string? accessToken = await GetKvitkiAccessTokenAsync();
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                _logger?.LogInformation("Токен доступа Kvitki получен успешно");
            }
            else
            {
                _logger?.LogInformation("Токен не получен, пробуем запросы без токена");
            }
            
            // Пробуем разные базовые URL для пагинации
            var baseApiUrls = new[]
            {
                "https://www.kvitki.by/api/events",
                "https://www.kvitki.by/api/searches",
                "https://www.kvitki.by/api/events/public",
                "https://www.kvitki.by/api/public/events",
                "https://www.kvitki.by/api/catalog",
                "https://www.kvitki.by/api/v1/events",
                "https://www.kvitki.by/api/v1/searches",
                "https://api.kvitki.by/events",
                "https://api.kvitki.by/searches"
            };

            string? workingBaseUrl = null;
            HttpClient? workingHttpClient = null;
            Exception? lastException = null;

            // Находим рабочий endpoint
            foreach (var baseUrl in baseApiUrls)
            {
                try
                {
                    _logger?.LogInformation("Попытка подключения к Kvitki: {Url}", baseUrl);
                    
                    var handler = new HttpClientHandler
                    {
                        AllowAutoRedirect = true,
                        MaxAutomaticRedirections = 5,
                        UseCookies = true,
                        CookieContainer = new System.Net.CookieContainer()
                    };
                    
                    var httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(30)
                    };
                    
                    // Получаем cookies
                    try
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                        httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                        httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                        
                        var homeResponse = await httpClient.GetAsync("https://www.kvitki.by/");
                        homeResponse.EnsureSuccessStatusCode();
                    }
                    catch
                    {
                        // Игнорируем ошибки получения cookies
                    }
                    
                    // Настраиваем заголовки для API
                    httpClient.DefaultRequestHeaders.Remove("Accept");
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
                    httpClient.DefaultRequestHeaders.Add("Referer", "https://www.kvitki.by/");
                    httpClient.DefaultRequestHeaders.Add("Origin", "https://www.kvitki.by");
                    
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    }

                    // Пробуем первый запрос с пагинацией (сначала GET, потом POST если не работает)
                    var testUrl = $"{baseUrl}?limit=50&offset=0";
                    HttpResponseMessage testResponse;
                    
                    try
                    {
                        testResponse = await httpClient.GetAsync(testUrl);
                    }
                    catch
                    {
                        // Если GET не работает, пробуем POST
                        _logger?.LogInformation("GET не сработал для {Url}, пробуем POST", baseUrl);
                        var postContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                        testResponse = await httpClient.PostAsync(baseUrl, postContent);
                    }
                    
                    var testContent = await testResponse.Content.ReadAsStringAsync();
                    var contentType = testResponse.Content.Headers.ContentType?.MediaType ?? "";
                    
                    // Детальное логирование для отладки
                    _logger?.LogInformation("Тест {Url}: Status={Status}, ContentType={ContentType}, ContentLength={Length}", 
                        baseUrl, testResponse.StatusCode, contentType, testContent.Length);
                    
                    if (!testResponse.IsSuccessStatusCode)
                    {
                        _logger?.LogWarning("❌ {Url} вернул статус {Status}. Ответ: {Response}", 
                            baseUrl, testResponse.StatusCode, 
                            testContent.Length > 500 ? testContent.Substring(0, 500) : testContent);
                        httpClient.Dispose();
                        continue;
                    }
                    
                    if (!contentType.Contains("json") && !testContent.TrimStart().StartsWith("{"))
                    {
                        _logger?.LogWarning("❌ {Url} вернул не JSON. ContentType={ContentType}, первые 200 символов: {Preview}", 
                            baseUrl, contentType, testContent.Length > 200 ? testContent.Substring(0, 200) : testContent);
                        httpClient.Dispose();
                        continue;
                    }
                    
                    if (string.IsNullOrWhiteSpace(testContent))
                    {
                        _logger?.LogWarning("❌ {Url} вернул пустой ответ", baseUrl);
                        httpClient.Dispose();
                        continue;
                    }
                    
                    // Проверяем, есть ли данные в ответе
                    var testData = JsonSerializer.Deserialize<KvitkiResponse>(testContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    var testAltData = JsonSerializer.Deserialize<KvitkiAlternativeResponse>(testContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    bool hasData = (testData?.ResponseData != null && testData.ResponseData.Any()) ||
                                   (testAltData?.Data != null && testAltData.Data.Any()) ||
                                   (testAltData?.Events != null && testAltData.Events.Any());
                    
                    if (!hasData)
                    {
                        _logger?.LogWarning("❌ {Url} вернул ответ без данных. ResponseStatus={Status}, ResponseData={HasData}, AltData={AltData}, AltEvents={AltEvents}", 
                            baseUrl,
                            testData?.ResponseStatus ?? "null",
                            testData?.ResponseData != null ? $"{testData.ResponseData.Count} элементов" : "null",
                            testAltData?.Data != null ? $"{testAltData.Data.Count} элементов" : "null",
                            testAltData?.Events != null ? $"{testAltData.Events.Count} элементов" : "null");
                        _logger?.LogInformation("Полный ответ (первые 1000 символов): {Response}", 
                            testContent.Length > 1000 ? testContent.Substring(0, 1000) : testContent);
                        httpClient.Dispose();
                        continue;
                    }
                    
                    workingBaseUrl = baseUrl;
                    workingHttpClient = httpClient;
                    _logger?.LogInformation("✅ Найден рабочий endpoint: {Url}", baseUrl);
                    break;
                }
                catch (HttpRequestException httpEx)
                {
                    _logger?.LogWarning(httpEx, "❌ HTTP ошибка при подключении к {Url}: {Message}", baseUrl, httpEx.Message);
                    lastException = httpEx;
                    continue;
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _logger?.LogWarning(timeoutEx, "❌ Таймаут при подключении к {Url}", baseUrl);
                    lastException = timeoutEx;
                    continue;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "❌ Не удалось подключиться к {Url}: {Message}", baseUrl, ex.Message);
                    lastException = ex;
                    continue;
                }
            }

            if (workingBaseUrl == null || workingHttpClient == null)
            {
                _logger?.LogError("Не удалось найти рабочий endpoint для Kvitki API. Последняя ошибка: {Error}", 
                    lastException?.Message ?? "неизвестная ошибка");
                return;
            }

            // ПАГИНАЦИЯ: Собираем все страницы данных (аналогично примеру с Spotify)
            List<KvitkiEvent> allEvents = new List<KvitkiEvent>();
            int limit = 50; // Размер страницы
            int offset = 0;
            int maxPages = 200; // Максимум страниц (200 * 50 = 10000 событий)
            int pageCount = 0;
            bool hasMore = true;

            try
            {
                while (hasMore && pageCount < maxPages)
                {
                    var apiUrl = $"{workingBaseUrl}?limit={limit}&offset={offset}";
                    _logger?.LogInformation("📄 Загрузка страницы {Page} из Kvitki (offset={Offset}, limit={Limit})", 
                        pageCount + 1, offset, limit);

                    try
                    {
                        var httpResponse = await workingHttpClient.GetAsync(apiUrl);
                        
                        if (!httpResponse.IsSuccessStatusCode)
                        {
                            _logger?.LogWarning("Kvitki API вернул статус {StatusCode} для страницы {Page}", 
                                httpResponse.StatusCode, pageCount + 1);
                            hasMore = false;
                            break;
                        }
                        
                        var response = await httpResponse.Content.ReadAsStringAsync();
                        var contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "";
                        
                        if (!contentType.Contains("json") && !response.TrimStart().StartsWith("{"))
                        {
                            _logger?.LogWarning("Kvitki API вернул не JSON для страницы {Page}", pageCount + 1);
                            hasMore = false;
                            break;
                        }
                        
                        if (string.IsNullOrWhiteSpace(response) || 
                            (response.Contains("\"responseStatus\"") && response.Contains("\"fail\"")))
                        {
                            _logger?.LogWarning("Kvitki API вернул пустой ответ или ошибку для страницы {Page}", pageCount + 1);
                            hasMore = false;
                            break;
                        }
                        
                        // Парсим ответ
                        var kvitkiData = JsonSerializer.Deserialize<KvitkiResponse>(response, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        List<KvitkiEvent>? pageEvents = null;
                        
                        if (kvitkiData != null && kvitkiData.ResponseData != null && kvitkiData.ResponseData.Any())
                        {
                            pageEvents = kvitkiData.ResponseData;
                        }
                        else
                        {
                            var alternativeData = JsonSerializer.Deserialize<KvitkiAlternativeResponse>(response, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                            
                            if (alternativeData?.Data != null && alternativeData.Data.Any())
                            {
                                pageEvents = alternativeData.Data;
                            }
                            else if (alternativeData?.Events != null && alternativeData.Events.Any())
                            {
                                pageEvents = alternativeData.Events;
                            }
                        }
                        
                        if (pageEvents == null || !pageEvents.Any())
                        {
                            _logger?.LogInformation("Страница {Page} пуста, завершаем пагинацию", pageCount + 1);
                            hasMore = false;
                            break;
                        }
                        
                        allEvents.AddRange(pageEvents);
                        _logger?.LogInformation("✅ Страница {Page}: получено {Count} событий (всего собрано: {Total})", 
                            pageCount + 1, pageEvents.Count, allEvents.Count);
                        
                        // Проверяем, есть ли еще данные
                        // Если получили меньше, чем limit, значит это последняя страница
                        if (pageEvents.Count < limit)
                        {
                            hasMore = false;
                        }
                        else
                        {
                            offset += limit;
                            pageCount++;
                            await Task.Delay(200); // Небольшая задержка между запросами
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Ошибка при загрузке страницы {Page} из Kvitki", pageCount + 1);
                        hasMore = false;
                        break;
                    }
                }
            }
            finally
            {
                workingHttpClient?.Dispose();
            }

            if (allEvents.Count == 0)
            {
                _logger?.LogWarning("Не удалось получить события из Kvitki API");
                return;
            }
            
            _logger?.LogInformation("🎯 Всего получено {Count} событий из Kvitki API (обработано {Pages} страниц)", 
                allEvents.Count, pageCount + 1);
            
            var eventsList = allEvents;

            var categories = await _context.Categories.ToListAsync();

            int addedCount = 0;
            int updatedCount = 0;
            
            // Фильтруем только развлечения с билетами из Kvitki
            var eventsToProcess = new List<KvitkiEvent>();
            int filteredOut = 0;
            
            foreach (var evt in eventsList)
            {
                if (IsEntertainmentEvent(evt))
                {
                    eventsToProcess.Add(evt);
                }
                else
                {
                    filteredOut++;
                    if (filteredOut <= 5) // Логируем первые 5 отфильтрованных для отладки
                    {
                        _logger?.LogDebug("❌ Отфильтровано событие: {Title} (категория: {Category})", 
                            evt.Title, evt.Category ?? "нет");
                    }
                }
            }
            
            _logger?.LogInformation("✅ Отфильтровано: {Filtered} событий-развлечений с билетами из {Total} (отклонено: {Rejected})", 
                eventsToProcess.Count, eventsList.Count, filteredOut);

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

                // Проверяем существующее место (case-insensitive)
                var existingPlace = await _context.Places
                    .FirstOrDefaultAsync(p => p.Name != null && p.Name.ToLower() == placeName.ToLower());

                if (existingPlace != null)
                {
                    updatedCount++;
                    continue;
                }

                var createPlaceDto = new CreatePlaceDto
                {
                    Name = placeName,
                    Description = (evt.Description ?? string.Empty).Length > 2000 ? (evt.Description ?? string.Empty).Substring(0, 2000) : (evt.Description ?? string.Empty),
                    Address = evt.Venue?.Name?.Length > 200 ? evt.Venue.Name.Substring(0, 200) : evt.Venue?.Name,
                    ImageUrl = evt.ImageUrl?.Length > 500 ? evt.ImageUrl.Substring(0, 500) : evt.ImageUrl,
                    Latitude = evt.Venue?.Latitude,
                    Longitude = evt.Venue?.Longitude,
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
                    if (addedCount % 10 == 0 || addedCount <= 5)
                    {
                        _logger?.LogInformation("✅ Добавлено {Count} новых мест из Kvitki (последнее: {Name})", addedCount, createPlaceDto.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "❌ Не удалось добавить место из Kvitki: {Name} - {Error}", createPlaceDto.Name, ex.Message);
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

    /// <summary>
    /// Проверяет, является ли событие развлечением с билетами (строгая фильтрация)
    /// </summary>
    private bool IsEntertainmentEvent(KvitkiEvent evt)
    {
        if (evt == null || string.IsNullOrWhiteSpace(evt.Title))
        {
            return false;
        }

        var text = (evt.Title + " " + (evt.Description ?? "") + " " + (evt.Category ?? "")).ToLower();
        
        // ОБЯЗАТЕЛЬНО должны быть билеты
        var ticketKeywords = new[]
        {
            "билет", "билеты", "билета", "билетов", "ticket", "tickets",
            "купить билет", "покупка билет", "продажа билет", "заказать билет",
            "билет на", "билеты на", "билет в", "билеты в", "билет к"
        };

        // ОБЯЗАТЕЛЬНО должны быть развлечения
        var entertainmentKeywords = new[]
        {
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

        // Строгие исключения - если есть эти слова, событие НЕ подходит
        var strictExcludeKeywords = new[]
        {
            "работа", "вакансия", "резюме", "трудоустройство", "найм",
            "обучение", "курсы", "школа", "университет", "колледж", "институт",
            "лечение", "медицина", "врач", "больница", "клиника", "диагностика", "терапия",
            "ремонт", "строительство", "отделка", "дизайн интерьера", "монтаж",
            "услуги", "сервис", "доставка", "перевозка", "такси", "грузоперевозки",
            "недвижимость", "квартира", "дом", "дача", "участок", "продажа квартир",
            "автомобиль", "машина", "транспорт", "запчасти", "автосервис",
            "одежда", "обувь", "аксессуары", "косметика", "магазин",
            "продажа", "покупка", "аренда", "снять", "купить", "продать", "обмен"
        };

        // Проверяем наличие билетов
        bool hasTicketKeyword = ticketKeywords.Any(keyword => text.Contains(keyword));
        
        // Проверяем наличие развлечений
        bool hasEntertainmentKeyword = entertainmentKeywords.Any(keyword => text.Contains(keyword));
        
        // СТРОГАЯ ФИЛЬТРАЦИЯ: должны быть И билеты, И развлечения
        if (!hasTicketKeyword || !hasEntertainmentKeyword)
        {
            return false;
        }
        
        // Проверяем строгие исключения
        bool hasStrictExcludeKeyword = strictExcludeKeywords.Any(keyword => text.Contains(keyword));
        if (hasStrictExcludeKeyword)
        {
            // Если есть исключающие слова, но они в контексте билетов/развлечений - проверяем дополнительно
            // Например, "купить билет на концерт" - это нормально, даже если есть слово "купить"
            if (text.Contains("купить") && hasTicketKeyword && hasEntertainmentKeyword)
            {
                // "купить билет" - это нормально
                return true;
            }
            // Другие исключения - отклоняем
            return false;
        }

        // Если есть и билеты, и развлечения, и нет исключений - подходит
        return true;
    }
}

// Модели для Kufar API
public class KufarResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("ads")]
    public List<KufarAd>? Ads { get; set; }
    public object? Pagination { get; set; }
    public int? Total { get; set; }
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
    [System.Text.Json.Serialization.JsonPropertyName("responseData")]
    public List<KvitkiEvent>? ResponseData { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("responseStatus")]
    public string? ResponseStatus { get; set; }
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

// Альтернативный формат ответа для /api/searches
public class KvitkiAlternativeResponse
{
    public List<KvitkiEvent>? Data { get; set; }
    public List<KvitkiEvent>? Events { get; set; }
    public List<KvitkiEvent>? Items { get; set; }
    public List<KvitkiEvent>? Results { get; set; }
    public string? Status { get; set; }
    public int? Total { get; set; }
}

// Модель для ответа с токенами
public class KvitkiTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
}

