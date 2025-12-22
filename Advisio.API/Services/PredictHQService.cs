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
    Task UpdateExistingPlacesAsync();
    Task UpdateEventTypesForExistingPlacesAsync();
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

    // Перевод городов и стран - ВСЕГДА переводит на русский, даже если это транслит
    private async Task<string> TranslateCityOrCountryAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || _translationService == null)
        {
            return text;
        }

        try
        {
            // Города и страны ВСЕГДА переводим на русский, даже если они выглядят как имена собственные
            var translated = await _translationService.TranslateToRussianAsync(text);
            
            // Если перевод не изменился или получился транслит, пробуем еще раз с более точным запросом
            if (translated == text || ContainsTranslit(translated))
            {
                // Пробуем добавить контекст для лучшего перевода
                var contextualText = $"{text} city";
                var contextualTranslation = await _translationService.TranslateToRussianAsync(contextualText);
                
                // Убираем слово "город" из перевода, если оно добавилось
                contextualTranslation = contextualTranslation
                    .Replace(" город", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(" city", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
                
                if (!ContainsTranslit(contextualTranslation) && contextualTranslation != text)
                {
                    return contextualTranslation;
                }
            }
            
            return translated;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при переводе города/страны '{Text}', используем оригинал", text);
            return text;
        }
    }

    // Умный перевод: не переводит имена собственные (названия мест, имена людей, названия брендов)
    private async Task<string> SmartTranslateAsync(string text, bool isProperNoun = false)
    {
        if (string.IsNullOrWhiteSpace(text) || _translationService == null)
        {
            return text;
        }

        // Если это имя собственное (название места, имя человека, название бренда), оставляем на английском
        if (isProperNoun)
        {
            return text;
        }

        // Проверяем, является ли текст именем собственным (названием места, бренда и т.д.)
        // Имена собственные обычно начинаются с заглавной буквы и содержат только латинские буквы
        if (text.Length > 0 && char.IsUpper(text[0]))
        {
            // Проверяем, не является ли это известным названием места или брендом
            var knownProperNouns = new[] { "vs", "at", "in", "and", "the", "of", "on", "for", "with", "theatre", "theater", "festival", "stage", "university", "college", "street", "avenue", "boulevard" };
            var words = text.Split(new[] { ' ', '-', '\'', ',', '.', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Если все слова начинаются с заглавной буквы или это известные слова, это имя собственное
            if (words.Length > 0 && words.All(w => w.Length > 0 && (char.IsUpper(w[0]) || knownProperNouns.Contains(w.ToLower()))))
            {
                return text; // Оставляем на английском
            }
            
            // Если текст содержит только латинские буквы и большинство слов начинается с заглавной, это имя собственное
            if (text.All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || c == '-' || c == '\'' || c == ',' || c == '.' || c == ':' || c == ';'))
            {
                var capitalizedWords = words.Count(w => w.Length > 0 && char.IsUpper(w[0]));
                if (capitalizedWords > words.Length / 2 && words.Length > 1)
                {
                    return text; // Оставляем на английском
                }
            }
        }

        try
        {
            var translated = await _translationService.TranslateToRussianAsync(text);
            
            // Проверяем, не получился ли транслит (например, "Moskva" вместо "Москва")
            // Если перевод содержит много латинских букв в словах, которые должны быть на русском, оставляем оригинал
            if (translated != text && ContainsTranslit(translated))
            {
                _logger?.LogDebug("Обнаружен транслит в переводе '{Original}' -> '{Translated}', оставляем оригинал", text, translated);
                return text;
            }
            
            return translated;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при переводе текста '{Text}', используем оригинал", text);
            return text;
        }
    }

    // Перевод адреса - переводит все части адреса на русский, убирает почтовые индексы
    // Формат: страна, город, улица, номер здания (без почтовых индексов)
    private async Task<string> TranslateAddressAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return RemovePostalCode(address ?? "");
        }

        try
        {
            // Сначала убираем почтовые индексы
            var cleanedAddress = RemovePostalCode(address);
            
            if (_translationService == null)
            {
                return cleanedAddress;
            }
            
            // Разбиваем адрес на части (по запятым)
            var parts = cleanedAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count == 0)
            {
                return cleanedAddress;
            }

            var translatedParts = new List<string>();
            
            foreach (var part in parts)
            {
                // Пропускаем почтовые индексы (только цифры 5-6 символов)
                if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d{5,6}$"))
                {
                    continue;
                }
                
                // Если часть содержит только цифры (номер дома), оставляем как есть
                if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d+$"))
                {
                    translatedParts.Add(part);
                    continue;
                }
                
                // Переводим каждую часть адреса на русский
                var translated = await _translationService.TranslateToRussianAsync(part);
                
                // Если получился транслит, пробуем еще раз с контекстом
                if (ContainsTranslit(translated) && translated == part)
                {
                    var contextual = await _translationService.TranslateToRussianAsync($"{part} address");
                    contextual = contextual.Replace(" адрес", "", StringComparison.OrdinalIgnoreCase)
                                           .Replace(" address", "", StringComparison.OrdinalIgnoreCase)
                                           .Trim();
                    if (!ContainsTranslit(contextual) && contextual != part)
                    {
                        translated = contextual;
                    }
                }
                
                translatedParts.Add(translated);
            }
            
            // Формируем адрес: страна, город, улица, номер здания
            return string.Join(", ", translatedParts);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при переводе адреса '{Address}', используем очищенный оригинал", address);
            return RemovePostalCode(address);
        }
    }

    // Убирает почтовые индексы из адреса (цифры типа 36117, 101000)
    private string RemovePostalCode(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return address;

        // Убираем почтовые индексы (5-6 цифр подряд, обычно в конце адреса или после запятой)
        // Паттерн: запятая, пробел, 5-6 цифр, пробел или конец строки
        var result = System.Text.RegularExpressions.Regex.Replace(
            address,
            @"\s*,\s*\d{5,6}(?:\s|$)",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // Также убираем почтовые индексы в начале строки или после пробела
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"(?:^|\s)\d{5,6}\s+",
            " ",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        
        // Убираем почтовые индексы, которые стоят отдельно (только цифры)
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\b\d{5,6}\b",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // Убираем лишние пробелы и запятые
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s*,\s*", ", ");
        result = result.Trim();

        return result;
    }

    // Проверяет, содержит ли текст транслит (латинские буквы в словах, которые должны быть на русском)
    private bool ContainsTranslit(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Если текст содержит кириллицу, это не транслит
        if (text.Any(c => c >= 0x0400 && c <= 0x04FF))
        {
            return false;
        }

        var words = text.Split(new[] { ' ', '\n', '\t', '.', ',', '!', '?', ':', ';', '-', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
        var latinWords = 0;
        var totalWords = 0;

        foreach (var word in words)
        {
            if (word.Length < 2)
                continue;

            totalWords++;
            // Если слово содержит только латинские буквы и цифры, это может быть транслит
            if (word.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || char.IsDigit(c)))
            {
                latinWords++;
            }
        }

        // Если более 30% слов - латинские, это вероятно транслит
        return totalWords > 0 && (double)latinWords / totalWords > 0.3;
    }

    // Определяет тип мероприятия по категории PredictHQ и описанию
    private string? DetermineEventType(string? phqCategory, string? description, string? title, List<PredictHQEntity>? entities = null)
    {
        var categoryLower = phqCategory?.ToLower() ?? "";
        var descriptionLower = description?.ToLower() ?? "";
        var titleLower = title?.ToLower() ?? "";
        
        // Получаем категории из entities (venue)
        var entityCategories = new List<string>();
        if (entities != null)
        {
            foreach (var entity in entities)
            {
                if (!string.IsNullOrWhiteSpace(entity.Category))
                {
                    entityCategories.Add(entity.Category.ToLower());
                }
            }
        }
        
        var allEntityCategories = string.Join(" ", entityCategories);
        var allText = $"{titleLower} {descriptionLower} {allEntityCategories}".ToLower();

        // Маппинг категорий PredictHQ на типы мероприятий (с большой буквы)
        var typeMapping = new Dictionary<string, string?>
        {
            { "concerts", "Концерт" },
            { "sports", "Турнир" },
            { "expos", "Выставка" },
            { "festivals", "Фестиваль" },
            { "performing-arts", "Спектакль" },
            { "conferences", "Образование" },
            { "community", null }, // Не определяем тип для community
            { "school-holidays", null },
            { "observances", null },
            { "public-holidays", null }
        };

        // Определяем тип по категории PredictHQ
        if (!string.IsNullOrWhiteSpace(categoryLower) && typeMapping.TryGetValue(categoryLower, out var typeFromCategory))
        {
            if (!string.IsNullOrWhiteSpace(typeFromCategory))
            {
                return typeFromCategory;
            }
        }

        // Анализируем категории venue из entities
        foreach (var entityCat in entityCategories)
        {
            if (entityCat.Contains("restaurant") || entityCat.Contains("cafe") || entityCat.Contains("food") || 
                entityCat.Contains("dining") || entityCat.Contains("bar") || entityCat.Contains("bistro"))
            {
                return "Ресторан";
            }
            if (entityCat.Contains("park") || entityCat.Contains("recreation") || entityCat.Contains("outdoor"))
            {
                return "Парк";
            }
            if (entityCat.Contains("education") || entityCat.Contains("school") || entityCat.Contains("university") || 
                entityCat.Contains("college") || entityCat.Contains("museum") || entityCat.Contains("library"))
            {
                return "Образование";
            }
            if (entityCat.Contains("theater") || entityCat.Contains("theatre") || entityCat.Contains("stage") || 
                entityCat.Contains("performing"))
            {
                return "Спектакль";
            }
            if (entityCat.Contains("cinema") || entityCat.Contains("movie") || entityCat.Contains("film") || 
                entityCat.Contains("multiplex"))
            {
                return "Кино";
            }
            if (entityCat.Contains("exhibition") || entityCat.Contains("gallery") || entityCat.Contains("art"))
            {
                return "Выставка";
            }
            if (entityCat.Contains("trail") || entityCat.Contains("hiking") || entityCat.Contains("nature") || 
                entityCat.Contains("eco") || entityCat.Contains("ecological"))
            {
                return "Эко тропа";
            }
        }

        // Анализируем текст (название и описание) для определения типа
        // ВАЖНО: Проверяем в порядке от более специфичных к менее специфичным
        
        // Турнир (проверяем ПЕРВЫМ, так как спортивные мероприятия могут содержать слова "парк", "стадион" и т.д.)
        // Добавляем все возможные спортивные термины, включая "vs", "против", названия лиг и т.д.
        if (allText.Contains("турнир") || allText.Contains("tournament") || allText.Contains("соревнование") || 
            allText.Contains("championship") || allText.Contains("чемпионат") || allText.Contains("матч") || 
            allText.Contains("match") || allText.Contains("игра") || allText.Contains("game") || 
            allText.Contains("спорт") || allText.Contains("sport") || allText.Contains("олимпиада") || 
            allText.Contains("olympic") || allText.Contains("кубок") || allText.Contains("cup") ||
            allText.Contains("чемпион") || allText.Contains("champion") || allText.Contains("лига") ||
            allText.Contains("league") || allText.Contains("стадион") || allText.Contains("stadium") ||
            allText.Contains("арена") || allText.Contains("arena") || allText.Contains("соревновани") ||
            allText.Contains("competition") || allText.Contains("финал") || allText.Contains("final") ||
            allText.Contains(" vs ") || allText.Contains(" против ") || allText.Contains("против") ||
            allText.Contains("хоккей") || allText.Contains("hockey") || allText.Contains("футбол") ||
            allText.Contains("football") || allText.Contains("баскетбол") || allText.Contains("basketball") ||
            allText.Contains("волейбол") || allText.Contains("volleyball") || allText.Contains("теннис") ||
            allText.Contains("tennis") || allText.Contains("whl") || allText.Contains("khl") ||
            allText.Contains("nhl") || allText.Contains("nba") || allText.Contains("uefa") ||
            allText.Contains("чемпионат") || allText.Contains("первенство") || allText.Contains("соревновани") ||
            allText.Contains("команда") || allText.Contains("team") || allText.Contains("клуб") ||
            allText.Contains("club") || allText.Contains("атлетика") || allText.Contains("athletics"))
        {
            return "Турнир";
        }
        
        // Ресторан
        if (allText.Contains("ресторан") || allText.Contains("кафе") || allText.Contains("restaurant") || 
            allText.Contains("cafe") || allText.Contains("bistro") || allText.Contains("dining") || 
            allText.Contains("food") || allText.Contains("кухня") || allText.Contains("меню"))
        {
            return "Ресторан";
        }
        
        // Концерт
        if (allText.Contains("концерт") || allText.Contains("concert") || allText.Contains("музыка") || 
            allText.Contains("music") || allText.Contains("музыкальный") || allText.Contains("выступление") || 
            allText.Contains("группа") || allText.Contains("band") || allText.Contains("singer"))
        {
            return "Концерт";
        }
        
        // Парк (проверяем ПОСЛЕ турнира, чтобы не перехватывать спортивные мероприятия)
        // Исключаем ВСЕ спортивные термины, чтобы не путать со стадионами/аренами
        var hasSportTerms = allText.Contains("стадион") || allText.Contains("stadium") ||
                           allText.Contains("арена") || allText.Contains("arena") ||
                           allText.Contains("спорт") || allText.Contains("sport") ||
                           allText.Contains("турнир") || allText.Contains("tournament") ||
                           allText.Contains("матч") || allText.Contains("match") ||
                           allText.Contains("соревнование") || allText.Contains("competition") ||
                           allText.Contains("чемпионат") || allText.Contains("championship") ||
                           allText.Contains("игра") || allText.Contains("game") ||
                           allText.Contains("олимпиада") || allText.Contains("olympic") ||
                           allText.Contains("кубок") || allText.Contains("cup") ||
                           allText.Contains("чемпион") || allText.Contains("champion") ||
                           allText.Contains("лига") || allText.Contains("league") ||
                           allText.Contains("финал") || allText.Contains("final") ||
                           allText.Contains(" vs ") || allText.Contains(" против ") ||
                           allText.Contains("против") || allText.Contains("хоккей") ||
                           allText.Contains("hockey") || allText.Contains("футбол") ||
                           allText.Contains("football") || allText.Contains("баскетбол") ||
                           allText.Contains("basketball") || allText.Contains("волейбол") ||
                           allText.Contains("volleyball") || allText.Contains("теннис") ||
                           allText.Contains("tennis") || allText.Contains("whl") ||
                           allText.Contains("khl") || allText.Contains("nhl") ||
                           allText.Contains("nba") || allText.Contains("uefa") ||
                           allText.Contains("первенство") || allText.Contains("команда") ||
                           allText.Contains("team") || allText.Contains("клуб") ||
                           allText.Contains("club") || allText.Contains("атлетика") ||
                           allText.Contains("athletics");
        
        if (!hasSportTerms && 
            (allText.Contains("парк") || allText.Contains("park") || allText.Contains("сквер") || 
             allText.Contains("сад") || allText.Contains("garden") || allText.Contains("recreation") || 
             allText.Contains("отдых") || allText.Contains("прогулка")))
        {
            return "Парк";
        }
        
        // Эко тропа
        if (allText.Contains("эко тропа") || allText.Contains("экотропа") || allText.Contains("тропа") || 
            allText.Contains("trail") || allText.Contains("hiking") || allText.Contains("поход") || 
            allText.Contains("природа") || allText.Contains("nature") || allText.Contains("экологический") || 
            allText.Contains("ecological") || allText.Contains("экскурсия") || allText.Contains("экология"))
        {
            return "Эко тропа";
        }
        
        // Образование
        if (allText.Contains("образование") || allText.Contains("education") || allText.Contains("университет") || 
            allText.Contains("university") || allText.Contains("школа") || allText.Contains("school") || 
            allText.Contains("колледж") || allText.Contains("college") || allText.Contains("лекция") || 
            allText.Contains("lecture") || allText.Contains("семинар") || allText.Contains("seminar") || 
            allText.Contains("конференция") || allText.Contains("conference") || allText.Contains("музей") || 
            allText.Contains("museum") || allText.Contains("библиотека") || allText.Contains("library") || 
            allText.Contains("обучение") || allText.Contains("training") || allText.Contains("курс") || 
            allText.Contains("course"))
        {
            return "Образование";
        }
        
        // Спектакль
        if (allText.Contains("спектакль") || allText.Contains("театр") || allText.Contains("theater") || 
            allText.Contains("theatre") || allText.Contains("постановка") || allText.Contains("play") || 
            allText.Contains("drama") || allText.Contains("драма") || allText.Contains("сцена") || 
            allText.Contains("stage") || allText.Contains("актер") || allText.Contains("actor") || 
            allText.Contains("режиссер") || allText.Contains("director"))
        {
            return "Спектакль";
        }
        
        // Кино
        if (allText.Contains("кино") || allText.Contains("фильм") || allText.Contains("movie") || 
            allText.Contains("film") || allText.Contains("cinema") || allText.Contains("кинопоказ") || 
            allText.Contains("screening") || allText.Contains("премьера") || allText.Contains("premiere") || 
            allText.Contains("кинотеатр") || allText.Contains("multiplex"))
        {
            return "Кино";
        }
        
        // Выставка
        if (allText.Contains("выставка") || allText.Contains("expo") || allText.Contains("exhibition") || 
            allText.Contains("галерея") || allText.Contains("gallery") || allText.Contains("экспозиция") || 
            allText.Contains("art") || allText.Contains("искусство") || allText.Contains("коллекция") || 
            allText.Contains("collection"))
        {
            return "Выставка";
        }
        
        // Фестиваль
        if (allText.Contains("фестиваль") || allText.Contains("festival") || allText.Contains("фест") || 
            allText.Contains("праздник") || allText.Contains("celebration") || allText.Contains("фест"))
        {
            return "Фестиваль";
        }

        return null;
    }

    // Маппинг категорий PredictHQ в категории базы данных
    private async Task<List<int>> MapPredictHQCategoryToDbCategoriesAsync(string? phqCategory)
    {
        var categoryIds = new List<int>();
        
        if (string.IsNullOrWhiteSpace(phqCategory))
            return categoryIds;

        var categories = await _context.Categories.ToListAsync();
        
        // Маппинг категорий PredictHQ в категории базы данных
        var categoryMapping = new Dictionary<string, string[]>
        {
            { "concerts", new[] { "концерт", "музыка", "выступление" } },
            { "sports", new[] { "спорт", "турнир", "соревнование", "матч", "игра" } },
            { "expos", new[] { "выставка", "экспозиция" } },
            { "festivals", new[] { "фестиваль", "праздник" } },
            { "performing-arts", new[] { "театр", "спектакль", "представление" } },
            { "conferences", new[] { "конференция", "семинар", "встреча" } },
            { "community", new[] { "сообщество", "встреча" } },
            { "school-holidays", new[] { "праздник", "каникулы" } },
            { "observances", new[] { "праздник", "событие" } },
            { "public-holidays", new[] { "праздник" } }
        };

        var phqCategoryLower = phqCategory.ToLower();
        
        // Ищем категорию по маппингу
        if (categoryMapping.TryGetValue(phqCategoryLower, out var keywords))
        {
            foreach (var keyword in keywords)
            {
                var category = categories.FirstOrDefault(c => 
                    c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                
                if (category != null && !categoryIds.Contains(category.Id))
                {
                    categoryIds.Add(category.Id);
                }
            }
        }

        // Если не нашли по маппингу, ищем по ActivityType категориям
        if (!categoryIds.Any())
        {
            var activityCategories = categories.Where(c => c.Type == CategoryType.ActivityType).ToList();
            
            // Определяем тип активности по категории PredictHQ
            if (phqCategoryLower.Contains("sport") || phqCategoryLower.Contains("game") || phqCategoryLower.Contains("match"))
            {
                var activeCategory = activityCategories.FirstOrDefault(c => 
                    c.Name.Contains("Активный", StringComparison.OrdinalIgnoreCase));
                if (activeCategory != null)
                    categoryIds.Add(activeCategory.Id);
            }
            else if (phqCategoryLower.Contains("expo") || phqCategoryLower.Contains("festival") || 
                     phqCategoryLower.Contains("concert") || phqCategoryLower.Contains("performing"))
            {
                var calmCategory = activityCategories.FirstOrDefault(c => 
                    c.Name.Contains("Спокойный", StringComparison.OrdinalIgnoreCase));
                if (calmCategory != null)
                    categoryIds.Add(calmCategory.Id);
            }
        }

        // По умолчанию добавляем "С друзьями"
        if (!categoryIds.Any())
        {
            var friendsCategory = categories.FirstOrDefault(c => 
                c.Name.Contains("С друзьями", StringComparison.OrdinalIgnoreCase));
            if (friendsCategory != null)
                categoryIds.Add(friendsCategory.Id);
        }

        return categoryIds;
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

                    // Логируем полный ответ API для первого города для анализа структуры
                    if (citiesProcessed == 1 && response.Length > 0)
                    {
                        _logger?.LogInformation("📋 ПРИМЕР ОТВЕТА PredictHQ API (первые 2000 символов):");
                        _logger?.LogInformation("{Response}", response.Substring(0, Math.Min(2000, response.Length)));
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
                        
                        // Логируем структуру первого события для анализа
                        if (citiesProcessed == 1 && data?.Results != null && data.Results.Any())
                        {
                            var firstEvent = data.Results.First();
                            _logger?.LogInformation("📊 СТРУКТУРА ПЕРВОГО СОБЫТИЯ:");
                            _logger?.LogInformation("  ID: {Id}", firstEvent.Id);
                            _logger?.LogInformation("  Title: {Title}", firstEvent.Title);
                            _logger?.LogInformation("  Description: {Description}", firstEvent.Description ?? "null");
                            _logger?.LogInformation("  Start: {Start}, StartLocal: {StartLocal}", firstEvent.Start, firstEvent.StartLocal ?? "null");
                            _logger?.LogInformation("  End: {End}, EndLocal: {EndLocal}", firstEvent.End, firstEvent.EndLocal ?? "null");
                            _logger?.LogInformation("  Geo.Address.Locality: {Locality}", firstEvent.Geo?.Address?.Locality ?? "null");
                            _logger?.LogInformation("  Geo.Address.FormattedAddress: {Address}", firstEvent.Geo?.Address?.FormattedAddress ?? "null");
                            _logger?.LogInformation("  Geo.Address.Country: {Country}", firstEvent.Geo?.Address?.Country ?? "null");
                            if (firstEvent.Entities != null && firstEvent.Entities.Any())
                            {
                                var venue = firstEvent.Entities.FirstOrDefault(e => e.Type == "venue");
                                _logger?.LogInformation("  Venue (from entities): {Name}, Address: {Address}", 
                                    venue?.Name ?? "null", venue?.FormattedAddress ?? "null");
                            }
                        }
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
            var placesToCategories = new Dictionary<Place, List<int>>(); // Временный список для отслеживания категорий новых мест
            
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
                    
                    // Если не найдено по названию, проверяем по координатам
                    // Приоритет: geo.geometry.coordinates > location (массив) > location (объект) > entities venue
                    decimal? lat = null;
                    decimal? lon = null;
                    
                    // 1. Пытаемся получить координаты из geo.geometry.coordinates (самый точный источник)
                    if (phqEvent.Geo?.Geometry?.Coordinates != null && phqEvent.Geo.Geometry.Coordinates.Count >= 2)
                    {
                        lon = (decimal)phqEvent.Geo.Geometry.Coordinates[0]; // longitude
                        lat = (decimal)phqEvent.Geo.Geometry.Coordinates[1]; // latitude
                    }
                    // 2. Если location - это массив [longitude, latitude]
                    else if (phqEvent.Location?.Lat.HasValue == true && phqEvent.Location.Lon.HasValue == true)
                    {
                        lat = (decimal)phqEvent.Location.Lat.Value;
                        lon = (decimal)phqEvent.Location.Lon.Value;
                    }
                    // 3. Пытаемся получить координаты из entities (venue)
                    else if (phqEvent.Entities != null && phqEvent.Entities.Any())
                    {
                        var venueEntity = phqEvent.Entities.FirstOrDefault(e => e.Type == "venue");
                        // Координаты venue могут быть в отдельном запросе, но пока используем то что есть
                    }
                    
                    if (existingPlace == null && lat.HasValue && lon.HasValue)
                    {
                        // Ищем места в радиусе ~100 метров (примерно 0.001 градуса)
                        var tolerance = 0.001m;
                        existingPlace = await _context.Places
                            .FirstOrDefaultAsync(p => 
                                p.Latitude.HasValue && p.Longitude.HasValue &&
                                Math.Abs(p.Latitude.Value - lat.Value) < tolerance &&
                                Math.Abs(p.Longitude.Value - lon.Value) < tolerance);
                    }

                    // Получаем данные о месте проведения
                    string? city = null;
                    string? country = null;
                    string? venueName = null;
                    string? venueAddress = null;
                    
                    // Пытаемся получить venue из entities
                    if (phqEvent.Entities != null && phqEvent.Entities.Any())
                    {
                        var venueEntity = phqEvent.Entities.FirstOrDefault(e => e.Type == "venue");
                        if (venueEntity != null)
                        {
                            venueName = venueEntity.Name;
                            venueAddress = venueEntity.FormattedAddress;
                        }
                    }
                    
                    // Получаем город и страну из geo.address
                    if (phqEvent.Geo?.Address != null)
                    {
                        city = phqEvent.Geo.Address.Locality;
                        country = phqEvent.Geo.Address.Country;
                        
                        if (string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(phqEvent.Geo.Address.FormattedAddress))
                        {
                            var addressParts = phqEvent.Geo.Address.FormattedAddress.Split(',');
                            if (addressParts.Length > 0)
                            {
                                country = addressParts.Last().Trim();
                            }
                        }
                    }
                    
                    if (string.IsNullOrWhiteSpace(venueName) && phqEvent.Location != null)
                    {
                        venueName = phqEvent.Location.Name;
                    }

                    // Получаем реальное описание события из API (убираем "Sourced from predicthq.com")
                    string? eventDescriptionText = phqEvent.Description;
                    if (!string.IsNullOrWhiteSpace(eventDescriptionText))
                    {
                        // Убираем информацию об источнике
                        eventDescriptionText = eventDescriptionText
                            .Replace("Sourced from predicthq.com", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("Source: predicthq.com", "", StringComparison.OrdinalIgnoreCase)
                            .Replace("Источник: predicthq.com", "", StringComparison.OrdinalIgnoreCase)
                            .Trim();
                        
                        // Если после удаления источника осталась пустая строка, очищаем
                        if (string.IsNullOrWhiteSpace(eventDescriptionText))
                        {
                            eventDescriptionText = null;
                        }
                    }

                    // Формируем полное описание события ПРИ ЗАПИСИ В БД
                    var descriptionParts = new List<string>();
                    
                    // 1. Реальное описание события из API (ВСЕГДА переводим на русский)
                    if (!string.IsNullOrWhiteSpace(eventDescriptionText))
                    {
                        // Описание ВСЕГДА переводим на русский, не используем SmartTranslateAsync (который может оставить на английском)
                        var translatedDescription = _translationService != null 
                            ? await _translationService.TranslateToRussianAsync(eventDescriptionText) 
                            : eventDescriptionText;
                        descriptionParts.Add(translatedDescription);
                    }
                    
                    // 2. Дата и время проведения (формат: "Дата и время: 12.01.2026, 9:00-19:00")
                    string? dateTimeInfo = null;
                    string? datePart = null;
                    string? timePart = null;
                    
                    // Получаем дату
                    DateTime? startDate = null;
                    if (!string.IsNullOrWhiteSpace(phqEvent.StartLocal))
                    {
                        if (DateTime.TryParse(phqEvent.StartLocal, out var startLocal))
                        {
                            startDate = startLocal;
                        }
                    }
                    else if (phqEvent.Start.HasValue)
                    {
                        startDate = phqEvent.Start.Value;
                    }
                    
                    if (startDate.HasValue)
                    {
                        datePart = startDate.Value.ToString("dd.MM.yyyy");
                        
                        // Получаем время начала (формат: 9:00, не 09:00)
                        var startTime = startDate.Value.ToString("H:mm");
                        
                        // Получаем время окончания
                        DateTime? endDate = null;
                        if (!string.IsNullOrWhiteSpace(phqEvent.EndLocal))
                        {
                            if (DateTime.TryParse(phqEvent.EndLocal, out var endLocal))
                            {
                                endDate = endLocal;
                            }
                        }
                        else if (phqEvent.End.HasValue)
                        {
                            endDate = phqEvent.End.Value;
                        }
                        
                        if (endDate.HasValue)
                        {
                            // Если даты разные, показываем обе даты
                            if (startDate.Value.Date != endDate.Value.Date)
                            {
                                var endDatePart = endDate.Value.ToString("dd.MM.yyyy");
                                var endTime = endDate.Value.ToString("H:mm");
                                dateTimeInfo = $"Дата и время: {datePart}, {startTime} - {endDatePart}, {endTime}";
                            }
                            else
                            {
                                // Одна дата, показываем диапазон времени
                                var endTime = endDate.Value.ToString("H:mm");
                                timePart = $"{startTime}-{endTime}";
                                dateTimeInfo = $"Дата и время: {datePart}, {timePart}";
                            }
                        }
                        else
                        {
                            // Только время начала
                            dateTimeInfo = $"Дата и время: {datePart}, {startTime}";
                        }
                    }
                    
                    // 3. Цена за вход (формат: "25 byn взрослый билет, 15 byn детский" или "бесплатно")
                    var priceParts = new List<string>();
                    
                    if (phqEvent.PriceMin.HasValue && phqEvent.PriceMax.HasValue)
                    {
                        var currency = !string.IsNullOrWhiteSpace(phqEvent.Currency) ? phqEvent.Currency.ToLower() : "byn";
                        if (phqEvent.PriceMin.Value == phqEvent.PriceMax.Value)
                        {
                            if (phqEvent.PriceMin.Value == 0)
                            {
                                priceParts.Add("бесплатно");
                            }
                            else
                            {
                                priceParts.Add($"{phqEvent.PriceMin.Value:F0} {currency}");
                            }
                        }
                        else
                        {
                            if (phqEvent.PriceMin.Value == 0)
                            {
                                priceParts.Add("бесплатно");
                            }
                            else
                            {
                                priceParts.Add($"{phqEvent.PriceMin.Value:F0} {currency}");
                            }
                            priceParts.Add($"{phqEvent.PriceMax.Value:F0} {currency}");
                        }
                    }
                    else if (phqEvent.PriceMin.HasValue)
                    {
                        var currency = !string.IsNullOrWhiteSpace(phqEvent.Currency) ? phqEvent.Currency.ToLower() : "byn";
                        if (phqEvent.PriceMin.Value == 0)
                        {
                            priceParts.Add("бесплатно");
                        }
                        else
                        {
                            priceParts.Add($"от {phqEvent.PriceMin.Value:F0} {currency}");
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(phqEvent.Price))
                    {
                        var priceLower = phqEvent.Price.ToLower();
                        if (priceLower.Contains("free") || priceLower.Contains("бесплатно") || priceLower == "0")
                        {
                            priceParts.Add("бесплатно");
                        }
                        else
                        {
                            priceParts.Add(phqEvent.Price);
                        }
                    }
                    
                    // Объединяем дату, время и цену в одну строку
                    if (!string.IsNullOrWhiteSpace(dateTimeInfo))
                    {
                        if (priceParts.Any())
                        {
                            var priceStr = string.Join(", ", priceParts);
                            descriptionParts.Add($"{dateTimeInfo}, {priceStr}");
                        }
                        else
                        {
                            descriptionParts.Add(dateTimeInfo);
                        }
                    }
                    else if (priceParts.Any())
                    {
                        descriptionParts.Add($"Стоимость: {string.Join(", ", priceParts)}");
                    }
                    
                    // Формируем итоговое описание (каждая часть на новой строке)
                    var eventDescription = descriptionParts.Any() 
                        ? string.Join("\n", descriptionParts) 
                        : "";
                    
                    // Получаем категории на основе category из API
                    var categoryIds = await MapPredictHQCategoryToDbCategoriesAsync(phqEvent.Category);
                    
                    // Определяем тип мероприятия
                    var eventType = DetermineEventType(phqEvent.Category, eventDescriptionText, phqEvent.Title, phqEvent.Entities);
                    
                    if (existingPlace != null)
                    {
                        // Обновляем существующее место
                        _logger?.LogDebug("🔄 Обновление существующего места: ID={PlaceId}, Name={Name}", 
                            existingPlace.Id, existingPlace.Name);
                        
                        // ВСЕГДА обновляем описание (даже если оно пустое, чтобы убрать старые данные)
                        existingPlace.Description = eventDescription ?? "";
                        
                        // Обновляем тип мероприятия
                        if (!string.IsNullOrWhiteSpace(eventType))
                        {
                            existingPlace.Type = eventType;
                        }
                        
                        // Название: для стран СНГ (Россия, Беларусь) ВСЕГДА переводим на русский
                        // Для других стран переводим только если транслитом
                        if (!string.IsNullOrWhiteSpace(phqEvent.Title))
                        {
                            var title = phqEvent.Title.Trim();
                            
                            // Определяем страну
                            var isCISCountry = false;
                            if (!string.IsNullOrWhiteSpace(country))
                            {
                                var countryLower = country.ToLower();
                                isCISCountry = countryLower.Contains("россия") || countryLower.Contains("russia") ||
                                             countryLower.Contains("беларусь") || countryLower.Contains("belarus") ||
                                             countryLower.Contains("russian federation");
                            }
                            
                            if (isCISCountry)
                            {
                                // Для стран СНГ ВСЕГДА переводим название на русский
                                var translatedTitle = _translationService != null 
                                    ? await _translationService.TranslateToRussianAsync(title) 
                                    : title;
                                existingPlace.Name = translatedTitle;
                            }
                            else if (ContainsTranslit(title))
                            {
                                // Для других стран переводим только если транслитом
                                var translatedTitle = await SmartTranslateAsync(title);
                                existingPlace.Name = translatedTitle;
                            }
                            else
                            {
                                // Оставляем как есть (может быть на английском или уже на русском)
                                existingPlace.Name = title;
                            }
                        }
                        
                        // Обновляем адрес ПРИ ЗАПИСИ В БД: убираем почтовые индексы и переводим на русский
                        if (phqEvent.Entities != null && phqEvent.Entities.Any())
                        {
                            var venueEntity = phqEvent.Entities.FirstOrDefault(e => e.Type == "venue");
                            if (venueEntity != null && !string.IsNullOrWhiteSpace(venueEntity.FormattedAddress))
                            {
                                // Убираем почтовые индексы ПРИ ЗАПИСИ В БД
                                var rawAddress = RemovePostalCode(venueEntity.FormattedAddress);
                                // Адрес ВСЕГДА переводим на русский ПРИ ЗАПИСИ В БД
                                existingPlace.Address = await TranslateAddressAsync(rawAddress);
                            }
                        }
                        else if (phqEvent.Location != null)
                        {
                            var rawAddress = phqEvent.Location.Name ?? phqEvent.Location.Address ?? existingPlace.Address;
                            if (!string.IsNullOrWhiteSpace(rawAddress))
                            {
                                // Убираем почтовые индексы ПРИ ЗАПИСИ В БД
                                rawAddress = RemovePostalCode(rawAddress);
                                // Адрес ВСЕГДА переводим на русский ПРИ ЗАПИСИ В БД
                                existingPlace.Address = await TranslateAddressAsync(rawAddress);
                            }
                        }
                        
                        // Обновляем координаты (приоритет: geo.geometry.coordinates > location)
                        if (phqEvent.Geo?.Geometry?.Coordinates != null && phqEvent.Geo.Geometry.Coordinates.Count >= 2)
                        {
                            existingPlace.Longitude = (decimal)phqEvent.Geo.Geometry.Coordinates[0]; // longitude
                            existingPlace.Latitude = (decimal)phqEvent.Geo.Geometry.Coordinates[1]; // latitude
                        }
                        else if (phqEvent.Location != null && phqEvent.Location.Lat.HasValue && phqEvent.Location.Lon.HasValue)
                        {
                            existingPlace.Latitude = (decimal)phqEvent.Location.Lat.Value;
                            existingPlace.Longitude = (decimal)phqEvent.Location.Lon.Value;
                        }
                        if (!string.IsNullOrEmpty(phqEvent.Thumbnail))
                        {
                            existingPlace.ImageUrl = phqEvent.Thumbnail;
                        }
                        
                        // Обновляем категории
                        if (categoryIds.Any())
                        {
                            foreach (var categoryId in categoryIds)
                            {
                                var placeCategoryExists = await _context.PlaceCategories
                                    .AnyAsync(pc => pc.PlaceId == existingPlace.Id && pc.CategoryId == categoryId);
                                
                                if (!placeCategoryExists)
                                {
                                    _context.PlaceCategories.Add(new PlaceCategory
                                    {
                                        PlaceId = existingPlace.Id,
                                        CategoryId = categoryId
                                    });
                                }
                            }
                        }
                        
                        existingPlace.UpdatedAt = DateTime.UtcNow;
                        updatedCount++;
                    }
                    else
                    {
                        // Создаем новое место
                        // Получаем адрес (убираем почтовые индексы и переводим на русский)
                        string? placeAddress = null;
                        if (phqEvent.Entities != null && phqEvent.Entities.Any())
                        {
                            var venueEntity = phqEvent.Entities.FirstOrDefault(e => e.Type == "venue");
                            if (venueEntity != null && !string.IsNullOrWhiteSpace(venueEntity.FormattedAddress))
                            {
                                var rawAddress = RemovePostalCode(venueEntity.FormattedAddress);
                                // Адрес ВСЕГДА переводим на русский
                                placeAddress = await TranslateAddressAsync(rawAddress);
                            }
                        }
                        if (string.IsNullOrWhiteSpace(placeAddress))
                        {
                            var rawAddress = phqEvent.Location?.Name ?? phqEvent.Location?.Address ?? "";
                            if (!string.IsNullOrWhiteSpace(rawAddress))
                            {
                                rawAddress = RemovePostalCode(rawAddress);
                                // Адрес ВСЕГДА переводим на русский (города и страны переводим отдельно)
                                placeAddress = await TranslateAddressAsync(rawAddress);
                            }
                        }
                        
                        // Если адрес пустой, формируем из geo.address и переводим
                        if (string.IsNullOrWhiteSpace(placeAddress) && phqEvent.Geo?.Address != null)
                        {
                            var addressParts = new List<string>();
                            if (!string.IsNullOrWhiteSpace(phqEvent.Geo.Address.FormattedAddress))
                            {
                                var rawAddress = RemovePostalCode(phqEvent.Geo.Address.FormattedAddress);
                                // Адрес ВСЕГДА переводим на русский (города и страны переводим отдельно)
                                placeAddress = await TranslateAddressAsync(rawAddress);
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(phqEvent.Geo.Address.Locality))
                                {
                                    // Город ВСЕГДА переводим на русский
                                    addressParts.Add(await TranslateCityOrCountryAsync(phqEvent.Geo.Address.Locality));
                                }
                                if (!string.IsNullOrWhiteSpace(phqEvent.Geo.Address.Region))
                                {
                                    // Регион ВСЕГДА переводим на русский
                                    addressParts.Add(await TranslateCityOrCountryAsync(phqEvent.Geo.Address.Region));
                                }
                                if (!string.IsNullOrWhiteSpace(phqEvent.Geo.Address.Country))
                                {
                                    // Страну ВСЕГДА переводим на русский
                                    addressParts.Add(await TranslateCityOrCountryAsync(phqEvent.Geo.Address.Country));
                                }
                                if (addressParts.Any())
                                    placeAddress = string.Join(" ", addressParts);
                            }
                        }
                        
                        // Получаем координаты (приоритет: geo.geometry.coordinates > location)
                        decimal? placeLat = null;
                        decimal? placeLon = null;
                        if (phqEvent.Geo?.Geometry?.Coordinates != null && phqEvent.Geo.Geometry.Coordinates.Count >= 2)
                        {
                            placeLon = (decimal)phqEvent.Geo.Geometry.Coordinates[0]; // longitude
                            placeLat = (decimal)phqEvent.Geo.Geometry.Coordinates[1]; // latitude
                        }
                        else if (phqEvent.Location != null && phqEvent.Location.Lat.HasValue && phqEvent.Location.Lon.HasValue)
                        {
                            placeLat = (decimal)phqEvent.Location.Lat.Value;
                            placeLon = (decimal)phqEvent.Location.Lon.Value;
                        }
                        
                        // Название: для стран СНГ (Россия, Беларусь) ВСЕГДА переводим на русский
                        // Для других стран переводим только если транслитом
                        var title = phqEvent.Title.Trim();
                        string finalTitle;
                        
                        // Определяем страну
                        var isCISCountry = false;
                        if (!string.IsNullOrWhiteSpace(country))
                        {
                            var countryLower = country.ToLower();
                            isCISCountry = countryLower.Contains("россия") || countryLower.Contains("russia") ||
                                         countryLower.Contains("беларусь") || countryLower.Contains("belarus") ||
                                         countryLower.Contains("russian federation");
                        }
                        
                        if (isCISCountry)
                        {
                            // Для стран СНГ ВСЕГДА переводим название на русский
                            finalTitle = _translationService != null 
                                ? await _translationService.TranslateToRussianAsync(title) 
                                : title;
                        }
                        else if (ContainsTranslit(title))
                        {
                            // Для других стран переводим только если транслитом
                            finalTitle = await SmartTranslateAsync(title);
                        }
                        else
                        {
                            // Оставляем как есть (может быть на английском или уже на русском)
                            finalTitle = title;
                        }
                        
                        var place = new Place
                        {
                            Name = finalTitle,
                            Description = eventDescription ?? "",
                            Type = eventType,
                            Address = placeAddress,
                            Latitude = placeLat,
                            Longitude = placeLon,
                            ImageUrl = phqEvent.Thumbnail ?? "",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Places.Add(place);
                        addedCount++;
                        
                        // Сохраняем категории для добавления после сохранения места
                        if (categoryIds.Any())
                        {
                            placesToCategories[place] = categoryIds;
                        }
                        
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
                            
                            // После сохранения добавляем категории для новых мест из текущего батча
                            var placesInBatch = placesToCategories.Keys.ToList();
                            foreach (var place in placesInBatch)
                            {
                                if (placesToCategories.TryGetValue(place, out var placeCategoryIds))
                                {
                                    foreach (var categoryId in placeCategoryIds)
                                    {
                                        var placeCategoryExists = await _context.PlaceCategories
                                            .AnyAsync(pc => pc.PlaceId == place.Id && pc.CategoryId == categoryId);
                                        
                                        if (!placeCategoryExists)
                                        {
                                            _context.PlaceCategories.Add(new PlaceCategory
                                            {
                                                PlaceId = place.Id,
                                                CategoryId = categoryId
                                            });
                                        }
                                    }
                                }
                            }
                            
                            if (_context.ChangeTracker.Entries<PlaceCategory>().Any(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added))
                            {
                                await _context.SaveChangesAsync();
                            }
                            
                            // Очищаем список обработанных мест
                            placesToCategories.Clear();
                            
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
                
                // Добавляем категории для оставшихся новых мест
                if (placesToCategories.Any())
                {
                    foreach (var kvp in placesToCategories)
                    {
                        var place = kvp.Key;
                        var placeCategoryIds = kvp.Value;
                        
                        foreach (var categoryId in placeCategoryIds)
                        {
                            var placeCategoryExists = await _context.PlaceCategories
                                .AnyAsync(pc => pc.PlaceId == place.Id && pc.CategoryId == categoryId);
                            
                            if (!placeCategoryExists)
                            {
                                _context.PlaceCategories.Add(new PlaceCategory
                                {
                                    PlaceId = place.Id,
                                    CategoryId = categoryId
                                });
                            }
                        }
                    }
                    
                    if (_context.ChangeTracker.Entries<PlaceCategory>().Any(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added))
                    {
                        await _context.SaveChangesAsync();
                    }
                    
                    placesToCategories.Clear();
                }
                
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

    // Обновляет все существующие места в базе данных, применяя новые правила перевода и форматирования
    public async Task UpdateExistingPlacesAsync()
    {
        try
        {
            _logger?.LogInformation("🔄 Начало обновления всех существующих мест в базе данных...");
            
            var allPlaces = await _context.Places.ToListAsync();
            _logger?.LogInformation("📊 Найдено мест для обновления: {Count}", allPlaces.Count);
            
            var updatedCount = 0;
            var errorCount = 0;
            
            foreach (var place in allPlaces)
            {
                try
                {
                    var hasChanges = false;
                    
                    // 1. Обновляем адрес: убираем почтовые индексы и переводим на русский
                    if (!string.IsNullOrWhiteSpace(place.Address))
                    {
                        var originalAddress = place.Address;
                        var cleanedAddress = RemovePostalCode(originalAddress);
                        var translatedAddress = await TranslateAddressAsync(cleanedAddress);
                        
                        if (translatedAddress != originalAddress)
                        {
                            place.Address = translatedAddress;
                            hasChanges = true;
                        }
                    }
                    
                    // 2. Обновляем описание: переводим города и страны на русский, если они в транслите
                    if (!string.IsNullOrWhiteSpace(place.Description))
                    {
                        var originalDescription = place.Description;
                        var updatedDescription = await UpdateDescriptionAsync(originalDescription);
                        
                        if (updatedDescription != originalDescription)
                        {
                            place.Description = updatedDescription;
                            hasChanges = true;
                        }
                    }
                    
                    if (hasChanges)
                    {
                        place.UpdatedAt = DateTime.UtcNow;
                        updatedCount++;
                        
                        if (updatedCount <= 10)
                        {
                            _logger?.LogDebug("✅ Обновлено место ID={Id}, Name={Name}", place.Id, place.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger?.LogWarning(ex, "⚠️ Ошибка при обновлении места ID={Id}: {Error}", place.Id, ex.Message);
                }
            }
            
            // Сохраняем все изменения
            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger?.LogInformation("💾 Сохранено изменений: {Updated} мест", updatedCount);
            }
            
            _logger?.LogInformation("✅ Обновление завершено: обновлено {Updated}, ошибок {Errors}, всего мест {Total}", 
                updatedCount, errorCount, allPlaces.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Критическая ошибка при обновлении существующих мест");
            throw;
        }
    }

    // Обновляет типы мероприятий для всех существующих мест в базе данных
    // Загружает события из PredictHQ API и определяет тип на основе категорий и описания
    public async Task UpdateEventTypesForExistingPlacesAsync()
    {
        try
        {
            _logger?.LogInformation("🔄 Начало обновления типов мероприятий для существующих мест...");
            
            var allPlaces = await _context.Places.ToListAsync();
            _logger?.LogInformation("📊 Найдено мест для обновления типов: {Count}", allPlaces.Count);
            
            if (allPlaces.Count == 0)
            {
                _logger?.LogInformation("ℹ️ Нет мест для обновления");
                return;
            }
            
            // Получаем API ключ
            var apiKey = _configuration["PredictHQ:ApiToken"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger?.LogError("❌ API ключ PredictHQ не найден в конфигурации");
                throw new Exception("API ключ PredictHQ не найден в конфигурации");
            }
            
            var updatedCount = 0;
            var notFoundCount = 0;
            var errorCount = 0;
            
            // Загружаем события из PredictHQ API для определения типов
            // Используем те же города, что и в LoadPlacesFromPredictHQAsync
            var cities = new[] { "Minsk", "Moscow", "Saint Petersburg", "Kiev", "Warsaw" };
            
            foreach (var city in cities)
            {
                try
                {
                    _logger?.LogInformation("🔍 Загрузка событий для города {City}...", city);
                    
                    var url = $"https://api.predicthq.com/v1/events/?q={Uri.EscapeDataString(city)}&limit=1000";
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    request.Headers.Add("Accept", "application/json");
                    
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new PredictHQLocationConverter());
                    
                    var data = JsonSerializer.Deserialize<PredictHQResponse>(responseContent, options);
                    
                    if (data?.Results == null || !data.Results.Any())
                    {
                        _logger?.LogInformation("ℹ️ Нет событий для города {City}", city);
                        continue;
                    }
                    
                    _logger?.LogInformation("📥 Получено {Count} событий для города {City}", data.Results.Count, city);
                    
                    // Обрабатываем каждое событие
                    foreach (var phqEvent in data.Results)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(phqEvent.Title))
                                continue;
                            
                            // Определяем тип мероприятия
                            var eventType = DetermineEventType(
                                phqEvent.Category, 
                                phqEvent.Description, 
                                phqEvent.Title, 
                                phqEvent.Entities);
                            
                            if (string.IsNullOrWhiteSpace(eventType))
                                continue;
                            
                            // Ищем соответствующее место в базе данных
                            // Сначала по названию
                            var matchingPlace = allPlaces.FirstOrDefault(p => 
                                !string.IsNullOrWhiteSpace(p.Name) && 
                                p.Name.Equals(phqEvent.Title, StringComparison.OrdinalIgnoreCase));
                            
                            // Если не нашли по названию, ищем по координатам
                            if (matchingPlace == null && phqEvent.Geo?.Geometry?.Coordinates != null && 
                                phqEvent.Geo.Geometry.Coordinates.Count >= 2)
                            {
                                var eventLon = (decimal)phqEvent.Geo.Geometry.Coordinates[0];
                                var eventLat = (decimal)phqEvent.Geo.Geometry.Coordinates[1];
                                
                                var tolerance = 0.001m; // ~100 метров
                                matchingPlace = allPlaces.FirstOrDefault(p => 
                                    p.Latitude.HasValue && p.Longitude.HasValue &&
                                    Math.Abs(p.Latitude.Value - eventLat) < tolerance &&
                                    Math.Abs(p.Longitude.Value - eventLon) < tolerance);
                            }
                            
                            if (matchingPlace != null)
                            {
                                // Обновляем тип, если он изменился, отсутствует или написан с маленькой буквы
                                var needsUpdate = string.IsNullOrWhiteSpace(matchingPlace.Type) || 
                                                 matchingPlace.Type != eventType ||
                                                 (matchingPlace.Type.Length > 0 && char.IsLower(matchingPlace.Type[0]));
                                
                                if (needsUpdate)
                                {
                                    matchingPlace.Type = eventType;
                                    matchingPlace.UpdatedAt = DateTime.UtcNow;
                                    updatedCount++;
                                    
                                    if (updatedCount <= 10)
                                    {
                                        _logger?.LogInformation("✅ Обновлен тип для места ID={Id}, Name={Name}, Type={Type}", 
                                            matchingPlace.Id, matchingPlace.Name, eventType);
                                    }
                                }
                            }
                            else
                            {
                                notFoundCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            if (errorCount <= 5)
                            {
                                _logger?.LogWarning(ex, "⚠️ Ошибка при обработке события {EventId}: {Error}", 
                                    phqEvent.Id, ex.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "⚠️ Ошибка при загрузке событий для города {City}: {Error}", city, ex.Message);
                }
            }
            
            // Также обновляем типы для мест, которые не были найдены в API, но имеют описание/название
            // Это поможет определить типы для мест, которые не были загружены из PredictHQ
            _logger?.LogInformation("🔍 Определение типов для мест без типа на основе названия, описания и категорий...");
            
            // Загружаем ВСЕ места с категориями для обновления типов
            // Обновляем ВСЕ места, включая те, у которых уже есть тип (чтобы исправить неправильные типы)
            var allPlacesWithCategories = await _context.Places
                .Include(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
                .ToListAsync();
            
            // Обрабатываем ВСЕ места, но приоритет отдаем местам без типа или с неправильным типом
            var placesToUpdate = allPlacesWithCategories
                .Where(p => string.IsNullOrWhiteSpace(p.Type) || 
                           (p.Type != null && p.Type.Length > 0 && char.IsLower(p.Type[0])) ||
                           p.Type == "Парк") // Также обновляем все места с типом "Парк", чтобы проверить правильность
                .ToList();
            
            foreach (var place in placesToUpdate)
            {
                try
                {
                    // Пропускаем только места с правильным типом (не "Парк", так как он может быть неправильным)
                    // Для мест с типом "Парк" перепроверяем, так как они могли быть определены неправильно
                    if (!string.IsNullOrWhiteSpace(place.Type) && 
                        place.Type.Length > 0 && 
                        char.IsUpper(place.Type[0]) && 
                        place.Type != "Парк")
                    {
                        continue;
                    }
                    
                    // Получаем категории места
                    var categoryNames = place.PlaceCategories?
                        .Where(pc => pc.Category != null)
                        .Select(pc => pc.Category!.Name)
                        .ToList() ?? new List<string>();
                    
                    var categoryText = string.Join(" ", categoryNames);
                    
                    // Формируем полный текст для анализа (название + описание + категории + адрес)
                    var fullText = $"{place.Name} {place.Description} {categoryText} {place.Address}".ToLower();
                    
                    // Определяем тип на основе полного текста
                    var eventType = DetermineEventType(null, fullText, place.Name, null);
                    
                    // Если не нашли по названию/описанию, пробуем по категориям
                    if (string.IsNullOrWhiteSpace(eventType) && categoryNames.Any())
                    {
                        var categoryTextLower = categoryText.ToLower();
                        
                        // Маппинг категорий на типы
                        if (categoryTextLower.Contains("ресторан") || categoryTextLower.Contains("кафе") || 
                            categoryTextLower.Contains("еда") || categoryTextLower.Contains("кухня") ||
                            categoryTextLower.Contains("dining") || categoryTextLower.Contains("food"))
                        {
                            eventType = "Ресторан";
                        }
                        else if (categoryTextLower.Contains("концерт") || categoryTextLower.Contains("музыка") || 
                                 categoryTextLower.Contains("выступление") || categoryTextLower.Contains("music") ||
                                 categoryTextLower.Contains("concert"))
                        {
                            eventType = "Концерт";
                        }
                        else if (categoryTextLower.Contains("образование") || categoryTextLower.Contains("обучение") || 
                                 categoryTextLower.Contains("лекция") || categoryTextLower.Contains("семинар") ||
                                 categoryTextLower.Contains("конференция") || categoryTextLower.Contains("музей") ||
                                 categoryTextLower.Contains("education") || categoryTextLower.Contains("university") ||
                                 categoryTextLower.Contains("school") || categoryTextLower.Contains("museum"))
                        {
                            eventType = "Образование";
                        }
                        else if (categoryTextLower.Contains("турнир") || categoryTextLower.Contains("спорт") || 
                                 categoryTextLower.Contains("соревнование") || categoryTextLower.Contains("матч") ||
                                 categoryTextLower.Contains("sport") || categoryTextLower.Contains("tournament") ||
                                 categoryTextLower.Contains("championship") || categoryTextLower.Contains("game") ||
                                 categoryTextLower.Contains("чемпионат") || categoryTextLower.Contains("стадион") ||
                                 categoryTextLower.Contains("stadium") || categoryTextLower.Contains("арена") ||
                                 categoryTextLower.Contains("arena") || categoryTextLower.Contains("против") ||
                                 categoryTextLower.Contains("хоккей") || categoryTextLower.Contains("hockey") ||
                                 categoryTextLower.Contains("футбол") || categoryTextLower.Contains("football") ||
                                 categoryTextLower.Contains("баскетбол") || categoryTextLower.Contains("basketball") ||
                                 categoryTextLower.Contains("волейбол") || categoryTextLower.Contains("volleyball") ||
                                 categoryTextLower.Contains("теннис") || categoryTextLower.Contains("tennis") ||
                                 categoryTextLower.Contains("команда") || categoryTextLower.Contains("team") ||
                                 categoryTextLower.Contains("клуб") || categoryTextLower.Contains("club"))
                        {
                            eventType = "Турнир";
                        }
                        else if (categoryTextLower.Contains("спектакль") || categoryTextLower.Contains("театр") || 
                                 categoryTextLower.Contains("постановка") || categoryTextLower.Contains("theater") ||
                                 categoryTextLower.Contains("theatre") || categoryTextLower.Contains("drama"))
                        {
                            eventType = "Спектакль";
                        }
                        else if (categoryTextLower.Contains("кино") || categoryTextLower.Contains("фильм") || 
                                 categoryTextLower.Contains("кинопоказ") || categoryTextLower.Contains("cinema") ||
                                 categoryTextLower.Contains("movie") || categoryTextLower.Contains("film"))
                        {
                            eventType = "Кино";
                        }
                        else if (categoryTextLower.Contains("выставка") || categoryTextLower.Contains("экспозиция") || 
                                 categoryTextLower.Contains("галерея") || categoryTextLower.Contains("exhibition") ||
                                 categoryTextLower.Contains("gallery") || categoryTextLower.Contains("art"))
                        {
                            eventType = "Выставка";
                        }
                        else if (categoryTextLower.Contains("фестиваль") || categoryTextLower.Contains("фест") || 
                                 categoryTextLower.Contains("праздник") || categoryTextLower.Contains("festival") ||
                                 categoryTextLower.Contains("celebration"))
                        {
                            eventType = "Фестиваль";
                        }
                    }
                    
                    // Если все еще не определили, пробуем более агрессивный поиск по полному тексту
                    if (string.IsNullOrWhiteSpace(eventType))
                    {
                        var allTextLower = fullText;
                        
                        // Более широкий поиск по ключевым словам
                        if (allTextLower.Contains("ресторан") || allTextLower.Contains("кафе") || 
                            allTextLower.Contains("bistro") || allTextLower.Contains("dining") ||
                            allTextLower.Contains("restaurant") || allTextLower.Contains("cafe") ||
                            allTextLower.Contains("food") || allTextLower.Contains("кухня") ||
                            allTextLower.Contains("меню") || allTextLower.Contains("bar"))
                        {
                            eventType = "Ресторан";
                        }
                        else if (allTextLower.Contains("концерт") || allTextLower.Contains("concert") ||
                                 allTextLower.Contains("музыка") || allTextLower.Contains("music") ||
                                 allTextLower.Contains("выступление") || allTextLower.Contains("группа") ||
                                 allTextLower.Contains("band") || allTextLower.Contains("singer"))
                        {
                            eventType = "Концерт";
                        }
                        else if (allTextLower.Contains("турнир") || allTextLower.Contains("tournament") ||
                                 allTextLower.Contains("соревнование") || allTextLower.Contains("championship") ||
                                 allTextLower.Contains("чемпионат") || allTextLower.Contains("матч") ||
                                 allTextLower.Contains("match") || allTextLower.Contains("игра") ||
                                 allTextLower.Contains("game") || allTextLower.Contains("спорт") ||
                                 allTextLower.Contains("sport") || allTextLower.Contains("олимпиада") ||
                                 allTextLower.Contains("olympic") || allTextLower.Contains("кубок") ||
                                 allTextLower.Contains("cup") || allTextLower.Contains("чемпион") ||
                                 allTextLower.Contains("champion") || allTextLower.Contains("лига") ||
                                 allTextLower.Contains("league") || allTextLower.Contains("стадион") ||
                                 allTextLower.Contains("stadium") || allTextLower.Contains("арена") ||
                                 allTextLower.Contains("arena") || allTextLower.Contains("соревновани") ||
                                 allTextLower.Contains("competition") || allTextLower.Contains("финал") ||
                                 allTextLower.Contains("final") || allTextLower.Contains(" vs ") ||
                                 allTextLower.Contains(" против ") || allTextLower.Contains("против") ||
                                 allTextLower.Contains("хоккей") || allTextLower.Contains("hockey") ||
                                 allTextLower.Contains("футбол") || allTextLower.Contains("football") ||
                                 allTextLower.Contains("баскетбол") || allTextLower.Contains("basketball") ||
                                 allTextLower.Contains("волейбол") || allTextLower.Contains("volleyball") ||
                                 allTextLower.Contains("теннис") || allTextLower.Contains("tennis") ||
                                 allTextLower.Contains("whl") || allTextLower.Contains("khl") ||
                                 allTextLower.Contains("nhl") || allTextLower.Contains("nba") ||
                                 allTextLower.Contains("uefa") || allTextLower.Contains("первенство") ||
                                 allTextLower.Contains("команда") || allTextLower.Contains("team") ||
                                 allTextLower.Contains("клуб") || allTextLower.Contains("club") ||
                                 allTextLower.Contains("атлетика") || allTextLower.Contains("athletics"))
                        {
                            eventType = "Турнир";
                        }
                        else
                        {
                            // Проверяем, есть ли спортивные термины
                            var hasSportTermsInText = allTextLower.Contains("стадион") || allTextLower.Contains("stadium") ||
                                                     allTextLower.Contains("арена") || allTextLower.Contains("arena") ||
                                                     allTextLower.Contains("спорт") || allTextLower.Contains("sport") ||
                                                     allTextLower.Contains("турнир") || allTextLower.Contains("tournament") ||
                                                     allTextLower.Contains("матч") || allTextLower.Contains("match") ||
                                                     allTextLower.Contains("соревнование") || allTextLower.Contains("competition") ||
                                                     allTextLower.Contains("чемпионат") || allTextLower.Contains("championship") ||
                                                     allTextLower.Contains("игра") || allTextLower.Contains("game") ||
                                                     allTextLower.Contains("олимпиада") || allTextLower.Contains("olympic") ||
                                                     allTextLower.Contains("кубок") || allTextLower.Contains("cup") ||
                                                     allTextLower.Contains("чемпион") || allTextLower.Contains("champion") ||
                                                     allTextLower.Contains("лига") || allTextLower.Contains("league") ||
                                                     allTextLower.Contains("финал") || allTextLower.Contains("final") ||
                                                     allTextLower.Contains(" vs ") || allTextLower.Contains(" против ") ||
                                                     allTextLower.Contains("против") || allTextLower.Contains("хоккей") ||
                                                     allTextLower.Contains("hockey") || allTextLower.Contains("футбол") ||
                                                     allTextLower.Contains("football") || allTextLower.Contains("баскетбол") ||
                                                     allTextLower.Contains("basketball") || allTextLower.Contains("волейбол") ||
                                                     allTextLower.Contains("volleyball") || allTextLower.Contains("теннис") ||
                                                     allTextLower.Contains("tennis") || allTextLower.Contains("whl") ||
                                                     allTextLower.Contains("khl") || allTextLower.Contains("nhl") ||
                                                     allTextLower.Contains("nba") || allTextLower.Contains("uefa") ||
                                                     allTextLower.Contains("первенство") || allTextLower.Contains("команда") ||
                                                     allTextLower.Contains("team") || allTextLower.Contains("клуб") ||
                                                     allTextLower.Contains("club") || allTextLower.Contains("атлетика") ||
                                                     allTextLower.Contains("athletics");
                            
                            // Парк только если НЕТ спортивных терминов
                            if (!hasSportTermsInText && 
                                (allTextLower.Contains("парк") || allTextLower.Contains("park") ||
                                 allTextLower.Contains("сквер") || allTextLower.Contains("сад") ||
                                 allTextLower.Contains("garden") || allTextLower.Contains("recreation") ||
                                 allTextLower.Contains("отдых") || allTextLower.Contains("прогулка")))
                            {
                                eventType = "Парк";
                            }
                        }
                        
                        // Продолжаем проверку других типов, если тип еще не определен
                        if (string.IsNullOrWhiteSpace(eventType))
                        {
                            if (allTextLower.Contains("образование") || allTextLower.Contains("education") ||
                                allTextLower.Contains("университет") || allTextLower.Contains("university") ||
                                allTextLower.Contains("школа") || allTextLower.Contains("school") ||
                                allTextLower.Contains("колледж") || allTextLower.Contains("college") ||
                                allTextLower.Contains("лекция") || allTextLower.Contains("lecture") ||
                                allTextLower.Contains("семинар") || allTextLower.Contains("seminar") ||
                                allTextLower.Contains("конференция") || allTextLower.Contains("conference") ||
                                allTextLower.Contains("музей") || allTextLower.Contains("museum") ||
                                allTextLower.Contains("библиотека") || allTextLower.Contains("library") ||
                                allTextLower.Contains("обучение") || allTextLower.Contains("training") ||
                                allTextLower.Contains("курс") || allTextLower.Contains("course"))
                            {
                                eventType = "Образование";
                            }
                            else if (allTextLower.Contains("спектакль") || allTextLower.Contains("театр") ||
                                     allTextLower.Contains("theater") || allTextLower.Contains("theatre") ||
                                     allTextLower.Contains("постановка") || allTextLower.Contains("play") ||
                                     allTextLower.Contains("drama") || allTextLower.Contains("драма") ||
                                     allTextLower.Contains("сцена") || allTextLower.Contains("stage") ||
                                     allTextLower.Contains("актер") || allTextLower.Contains("actor") ||
                                     allTextLower.Contains("режиссер") || allTextLower.Contains("director"))
                            {
                                eventType = "Спектакль";
                            }
                            else if (allTextLower.Contains("кино") || allTextLower.Contains("фильм") ||
                                     allTextLower.Contains("movie") || allTextLower.Contains("film") ||
                                     allTextLower.Contains("cinema") || allTextLower.Contains("кинопоказ") ||
                                     allTextLower.Contains("screening") || allTextLower.Contains("премьера") ||
                                     allTextLower.Contains("premiere") || allTextLower.Contains("кинотеатр") ||
                                     allTextLower.Contains("multiplex"))
                            {
                                eventType = "Кино";
                            }
                            else if (allTextLower.Contains("выставка") || allTextLower.Contains("expo") ||
                                     allTextLower.Contains("exhibition") || allTextLower.Contains("галерея") ||
                                     allTextLower.Contains("gallery") || allTextLower.Contains("экспозиция") ||
                                     allTextLower.Contains("art") || allTextLower.Contains("искусство") ||
                                     allTextLower.Contains("коллекция") || allTextLower.Contains("collection"))
                            {
                                eventType = "Выставка";
                            }
                            else if (allTextLower.Contains("фестиваль") || allTextLower.Contains("festival") ||
                                     allTextLower.Contains("фест") || allTextLower.Contains("праздник") ||
                                     allTextLower.Contains("celebration"))
                            {
                                eventType = "Фестиваль";
                            }
                            else if (allTextLower.Contains("эко тропа") || allTextLower.Contains("экотропа") ||
                                     allTextLower.Contains("тропа") || allTextLower.Contains("trail") ||
                                     allTextLower.Contains("hiking") || allTextLower.Contains("поход") ||
                                     allTextLower.Contains("природа") || allTextLower.Contains("nature") ||
                                     allTextLower.Contains("экологический") || allTextLower.Contains("ecological") ||
                                     allTextLower.Contains("экскурсия") || allTextLower.Contains("экология"))
                            {
                                eventType = "Эко тропа";
                            }
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(eventType))
                    {
                        // Обновляем тип, если:
                        // 1. Тип отсутствует
                        // 2. Тип написан с маленькой буквы
                        // 3. Тип изменился (например, был "Парк", а должен быть "Турнир")
                        var needsUpdate = string.IsNullOrWhiteSpace(place.Type) || 
                                         place.Type != eventType ||
                                         (place.Type.Length > 0 && char.IsLower(place.Type[0]));
                        
                        if (needsUpdate)
                        {
                            var oldType = place.Type ?? "нет";
                            place.Type = eventType;
                            place.UpdatedAt = DateTime.UtcNow;
                            updatedCount++;
                            
                            if (updatedCount <= 30)
                            {
                                _logger?.LogInformation("✅ Обновлен тип для места ID={Id}, Name={Name}, OldType={OldType}, NewType={NewType}, Categories={Categories}", 
                                    place.Id, place.Name, oldType, eventType, categoryText);
                            }
                        }
                    }
                    else
                    {
                        // Логируем места, для которых не удалось определить тип
                        _logger?.LogDebug("⚠️ Не удалось определить тип для места ID={Id}, Name={Name}, Description={Description}, Categories={Categories}", 
                            place.Id, place.Name, 
                            place.Description?.Substring(0, Math.Min(100, place.Description?.Length ?? 0)) ?? "", 
                            categoryText);
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (errorCount <= 10)
                    {
                        _logger?.LogWarning(ex, "⚠️ Ошибка при определении типа для места ID={Id}: {Error}", 
                            place.Id, ex.Message);
                    }
                }
            }
            
            // Сохраняем все изменения
            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger?.LogInformation("💾 Сохранено изменений: {Updated} мест", updatedCount);
            }
            
            _logger?.LogInformation("✅ Обновление типов завершено: обновлено {Updated}, не найдено {NotFound}, ошибок {Errors}, всего мест {Total}", 
                updatedCount, notFoundCount, errorCount, allPlaces.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "❌ Критическая ошибка при обновлении типов мероприятий");
            throw;
        }
    }

    // Обновляет описание, переводя города и страны на русский, убирая лишние строки
    private async Task<string> UpdateDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        try
        {
            // Разбиваем описание на строки
            var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
            
            var updatedLines = new List<string>();
            
            foreach (var line in lines)
            {
                // Пропускаем строки, которые являются только цифрами (почтовые индексы)
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{5,6}$"))
                {
                    continue; // Пропускаем почтовые индексы
                }
                
                // Пропускаем строки, которые являются только названиями городов/стран на английском (они будут в строке "Место:")
                if (line.All(c => char.IsLetter(c) || char.IsWhiteSpace(c)) && 
                    (line.Equals("Moscow", StringComparison.OrdinalIgnoreCase) || 
                     line.Equals("Moskva", StringComparison.OrdinalIgnoreCase) ||
                     line.Equals("Russia", StringComparison.OrdinalIgnoreCase) ||
                     line.Equals("Russian Federation", StringComparison.OrdinalIgnoreCase) ||
                     line.Equals("Belarus", StringComparison.OrdinalIgnoreCase) ||
                     line.Equals("United States", StringComparison.OrdinalIgnoreCase) ||
                     line.Equals("USA", StringComparison.OrdinalIgnoreCase)))
                {
                    continue; // Пропускаем отдельные строки с городами/странами
                }
                
                // Обрабатываем строку "Место:"
                if (line.Contains("Место:", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, @"Место:\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var locationPart = match.Groups[1].Value;
                        // Переводим города и страны в части места
                        var translatedLocation = await TranslateLocationInDescriptionAsync(locationPart);
                        updatedLines.Add($"Место: {translatedLocation}");
                    }
                    else
                    {
                        updatedLines.Add(line.Replace("<b>", "").Replace("</b>", "")); // Убираем HTML теги
                    }
                }
                else if (line.Contains("Дата и время:", StringComparison.OrdinalIgnoreCase))
                {
                    // Оставляем как есть
                    updatedLines.Add(line);
                }
                else
                {
                    // Для остальных строк переводим города и страны на русский
                    var updatedLine = line;
                    
                    // Заменяем известные города и страны
                    var replacements = new Dictionary<string, string>
                    {
                        { "Moskva", "Москва" },
                        { "Moscow", "Москва" },
                        { "Minsk", "Минск" },
                        { "Russia", "Россия" },
                        { "Russian Federation", "Российская Федерация" },
                        { "Belarus", "Беларусь" },
                        { "United States", "США" },
                        { "USA", "США" },
                        { "US", "США" },
                        { "Myasnitskaya Ulitsa", "Мясницкая улица" },
                        { "Myasnitskaya", "Мясницкая" }
                    };
                    
                    foreach (var replacement in replacements)
                    {
                        updatedLine = System.Text.RegularExpressions.Regex.Replace(
                            updatedLine,
                            $@"\b{System.Text.RegularExpressions.Regex.Escape(replacement.Key)}\b",
                            replacement.Value,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    }
                    
                    // Если строка содержит транслит, пытаемся перевести
                    if (ContainsTranslit(updatedLine) && _translationService != null)
                    {
                        try
                        {
                            var translated = await _translationService.TranslateToRussianAsync(updatedLine);
                            if (!ContainsTranslit(translated))
                            {
                                updatedLine = translated;
                            }
                        }
                        catch
                        {
                            // Игнорируем ошибки перевода
                        }
                    }
                    
                    updatedLines.Add(updatedLine);
                }
            }
            
            return string.Join("\n", updatedLines);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при обновлении описания, используем оригинал");
            return description;
        }
    }

    // Переводит города и страны в части описания с местом
    private async Task<string> TranslateLocationInDescriptionAsync(string locationText)
    {
        if (string.IsNullOrWhiteSpace(locationText))
        {
            return locationText;
        }

        try
        {
            // Разбиваем на части по запятым
            var parts = locationText.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            
            var translatedParts = new List<string>();
            
            foreach (var part in parts)
            {
                // Пропускаем почтовые индексы (только цифры)
                if (System.Text.RegularExpressions.Regex.IsMatch(part, @"^\d{5,6}$"))
                {
                    continue; // Пропускаем почтовые индексы
                }
                
                // Если часть содержит только латинские буквы и выглядит как город/страна, переводим
                if (part.All(c => char.IsLetter(c) || char.IsWhiteSpace(c) || c == '-' || c == '\''))
                {
                    var translated = await TranslateCityOrCountryAsync(part);
                    translatedParts.Add(translated);
                }
                else
                {
                    // Если это название места (содержит цифры, специальные символы), не переводим
                    // Но переводим части адреса типа "Myasnitskaya Ulitsa 20" -> "Мясницкая улица 20"
                    var addressParts = part.Split(' ');
                    var translatedAddressParts = new List<string>();
                    
                    foreach (var addressPart in addressParts)
                    {
                        if (addressPart.All(c => char.IsLetter(c) || c == '-'))
                        {
                            // Это слово, пытаемся перевести
                            var translated = await TranslateCityOrCountryAsync(addressPart);
                            translatedAddressParts.Add(translated);
                        }
                        else
                        {
                            // Это число или специальный символ, оставляем как есть
                            translatedAddressParts.Add(addressPart);
                        }
                    }
                    
                    translatedParts.Add(string.Join(" ", translatedAddressParts));
                }
            }
            
            return string.Join(", ", translatedParts);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при переводе места в описании, используем оригинал");
            return locationText;
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
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("category")]
    public string? Category { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("location")]
    public PredictHQLocation? Location { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("venue")]
    public PredictHQVenue? Venue { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("start")]
    public DateTime? Start { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("end")]
    public DateTime? End { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("rank")]
    public int? Rank { get; set; }
    
    // Дополнительные поля, которые могут содержать описание
    [System.Text.Json.Serialization.JsonPropertyName("entity")]
    public string? Entity { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("phq_attendance")]
    public int? PhqAttendance { get; set; }
    
    // phq_labels может быть массивом строк или объектов, поэтому просто игнорируем это поле
    // [System.Text.Json.Serialization.JsonPropertyName("phq_labels")]
    // public List<object>? PhqLabels { get; set; }
    
    // Дополнительные поля из API
    [System.Text.Json.Serialization.JsonPropertyName("entities")]
    public List<PredictHQEntity>? Entities { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("start_local")]
    public string? StartLocal { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("end_local")]
    public string? EndLocal { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("geo")]
    public PredictHQGeo? Geo { get; set; } // geo - это отдельное поле на уровне события, не внутри location
    
    // Поля для цены
    [System.Text.Json.Serialization.JsonPropertyName("price")]
    public string? Price { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("price_min")]
    public decimal? PriceMin { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("price_max")]
    public decimal? PriceMax { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public class PredictHQVenue
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("address")]
    public string? Address { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("city")]
    public string? City { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("country")]
    public string? Country { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("state")]
    public string? State { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("lat")]
    public double? Lat { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("lon")]
    public double? Lon { get; set; }
}

public class PredictHQLocation
{
    [System.Text.Json.Serialization.JsonPropertyName("lat")]
    public double? Lat { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("lon")]
    public double? Lon { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("address")]
    public string? Address { get; set; }
    
    // Дополнительные поля из PredictHQ API для локации
    [System.Text.Json.Serialization.JsonPropertyName("country")]
    public string? Country { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("geo")]
    public PredictHQGeo? Geo { get; set; }
}

public class PredictHQGeo
{
    [System.Text.Json.Serialization.JsonPropertyName("address")]
    public PredictHQAddress? Address { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("geometry")]
    public PredictHQGeometry? Geometry { get; set; }
}

public class PredictHQGeometry
{
    [System.Text.Json.Serialization.JsonPropertyName("coordinates")]
    public List<double>? Coordinates { get; set; } // [longitude, latitude] в формате GeoJSON
    
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class PredictHQAddress
{
    [System.Text.Json.Serialization.JsonPropertyName("locality")]
    public string? Locality { get; set; } // Город
    
    [System.Text.Json.Serialization.JsonPropertyName("region")]
    public string? Region { get; set; } // Регион/область
    
    [System.Text.Json.Serialization.JsonPropertyName("country")]
    public string? Country { get; set; } // Страна
    
    [System.Text.Json.Serialization.JsonPropertyName("country_code")]
    public string? CountryCode { get; set; } // Код страны
    
    [System.Text.Json.Serialization.JsonPropertyName("formatted_address")]
    public string? FormattedAddress { get; set; } // Полный адрес
    
    [System.Text.Json.Serialization.JsonPropertyName("postcode")]
    public string? Postcode { get; set; } // Почтовый индекс
}

public class PredictHQEntity
{
    [System.Text.Json.Serialization.JsonPropertyName("entity_id")]
    public string? EntityId { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string? Type { get; set; } // "venue", "event-group" и т.д.
    
    [System.Text.Json.Serialization.JsonPropertyName("formatted_address")]
    public string? FormattedAddress { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("category")]
    public string? Category { get; set; }
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

        // Если location - это объект, используем стандартную десериализацию
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Используем стандартный десериализатор для объекта, чтобы получить все поля включая geo
            var jsonDoc = System.Text.Json.JsonDocument.ParseValue(ref reader);
            var root = jsonDoc.RootElement;
            
            if (root.TryGetProperty("lat", out var latElement) && latElement.ValueKind == JsonValueKind.Number)
            {
                location.Lat = latElement.GetDouble();
            }
            
            if (root.TryGetProperty("lon", out var lonElement) && lonElement.ValueKind == JsonValueKind.Number)
            {
                location.Lon = lonElement.GetDouble();
            }
            
            if (root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
            {
                location.Name = nameElement.GetString();
            }
            
            if (root.TryGetProperty("address", out var addressElement) && addressElement.ValueKind == JsonValueKind.String)
            {
                location.Address = addressElement.GetString();
            }
            
            if (root.TryGetProperty("country", out var countryElement) && countryElement.ValueKind == JsonValueKind.String)
            {
                location.Country = countryElement.GetString();
            }
            
            // Обрабатываем вложенный объект geo
            if (root.TryGetProperty("geo", out var geoElement) && geoElement.ValueKind == JsonValueKind.Object)
            {
                location.Geo = new PredictHQGeo();
                
                if (geoElement.TryGetProperty("address", out var geoAddressElement) && geoAddressElement.ValueKind == JsonValueKind.Object)
                {
                    location.Geo.Address = new PredictHQAddress();
                    
                    if (geoAddressElement.TryGetProperty("locality", out var localityElement) && localityElement.ValueKind == JsonValueKind.String)
                    {
                        location.Geo.Address.Locality = localityElement.GetString();
                    }
                    
                    if (geoAddressElement.TryGetProperty("region", out var regionElement) && regionElement.ValueKind == JsonValueKind.String)
                    {
                        location.Geo.Address.Region = regionElement.GetString();
                    }
                    
                    if (geoAddressElement.TryGetProperty("country", out var geoCountryElement) && geoCountryElement.ValueKind == JsonValueKind.String)
                    {
                        location.Geo.Address.Country = geoCountryElement.GetString();
                    }
                    
                    if (geoAddressElement.TryGetProperty("country_code", out var countryCodeElement) && countryCodeElement.ValueKind == JsonValueKind.String)
                    {
                        location.Geo.Address.CountryCode = countryCodeElement.GetString();
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

