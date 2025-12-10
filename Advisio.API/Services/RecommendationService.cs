using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public class RecommendationService : IRecommendationService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;
    private readonly ISurveyService _surveyService;

    public RecommendationService(
        AdvisioDbContext context,
        IPlaceService placeService,
        ISurveyService surveyService)
    {
        _context = context;
        _placeService = placeService;
        _surveyService = surveyService;
    }

    public async Task<List<RecommendationDto>> GetRecommendationsAsync(int userId, int count = 9)
    {
        // Проверяем, есть ли рекомендации для пользователя
        var existingRecommendations = await _context.Recommendations
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.CreatedAt)
            .Take(count)
            .Include(r => r.Place)
                .ThenInclude(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
            .Include(r => r.Place)
                .ThenInclude(p => p.Likes)
            .ToListAsync();

        // Проверяем, нужно ли регенерировать рекомендации
        bool shouldRegenerate = false;

        if (!existingRecommendations.Any())
        {
            // Если рекомендаций нет, генерируем их
            shouldRegenerate = true;
        }
        else
        {
            // Проверяем, изменились ли лайки или избранные после генерации рекомендаций
            var oldestRecommendationTime = existingRecommendations.Min(r => r.CreatedAt);
            
            // Получаем время последнего изменения лайков или избранных
            var lastLikeChange = await _context.Likes
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            var lastFavoriteChange = await _context.Favorites
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.CreatedAt)
                .FirstOrDefaultAsync();

            var lastChange = new[] { lastLikeChange, lastFavoriteChange }
                .Where(d => d != default(DateTime))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            // Проверяем, есть ли у пользователя лайки или избранные
            var hasLikesOrFavorites = await _context.Likes
                .AnyAsync(l => l.UserId == userId) || 
                await _context.Favorites
                .AnyAsync(f => f.UserId == userId);

            // Если были изменения после генерации рекомендаций, регенерируем
            if (lastChange > oldestRecommendationTime)
            {
                shouldRegenerate = true;
            }
            // Если у пользователя нет лайков и избранных, но рекомендации есть - регенерируем
            else if (!hasLikesOrFavorites && existingRecommendations.Any())
            {
                shouldRegenerate = true;
            }
            // Также регенерируем, если рекомендации старше 1 часа
            else if (oldestRecommendationTime < DateTime.UtcNow.AddHours(-1))
            {
                shouldRegenerate = true;
            }
        }

        if (shouldRegenerate)
        {
            await GenerateRecommendationsAsync(userId);
        }

        // Получаем рекомендации (новые или существующие)
        var recommendations = await _context.Recommendations
            .Where(r => r.UserId == userId)
            .Include(r => r.Place)
                .ThenInclude(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
            .Include(r => r.Place)
                .ThenInclude(p => p.Likes)
            .ToListAsync();

        // Получаем количество лайков для каждого места
        var placeIds = recommendations.Select(r => r.PlaceId).ToList();
        var likesCounts = await _context.Likes
            .Where(l => placeIds.Contains(l.PlaceId))
            .GroupBy(l => l.PlaceId)
            .Select(g => new { PlaceId = g.Key, LikesCount = g.Count() })
            .ToDictionaryAsync(x => x.PlaceId, x => x.LikesCount);

        // Сортируем по score, затем по количеству лайков, затем по дате создания
        recommendations = recommendations
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => likesCounts.GetValueOrDefault(r.PlaceId, 0))
            .ThenByDescending(r => r.CreatedAt)
            .Take(count)
            .ToList();

        return recommendations.Select(r => new RecommendationDto
        {
            Id = r.Id,
            Place = _placeService.GetPlaceByIdAsync(r.PlaceId, userId).Result!,
            Score = r.Score,
            Reason = r.Reason,
            CreatedAt = r.CreatedAt,
            IsViewed = r.IsViewed
        }).ToList();
    }

    public async Task GenerateRecommendationsAsync(int userId)
    {
        // Удаляем старые рекомендации
        var oldRecommendations = await _context.Recommendations
            .Where(r => r.UserId == userId)
            .ToListAsync();
        _context.Recommendations.RemoveRange(oldRecommendations);

        // Получаем места, которые пользователь уже лайкнул или добавил в избранное
        // НЕ исключаем их из рекомендаций, а даем им бонус
        var userLikedPlaceIds = (await _context.Likes
            .Where(l => l.UserId == userId)
            .Select(l => l.PlaceId)
            .ToListAsync()).ToHashSet();

        var userFavoritePlaceIds = (await _context.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.PlaceId)
            .ToListAsync()).ToHashSet();

        // Словарь для хранения рекомендаций (PlaceId -> Recommendation)
        var recommendationsDict = new Dictionary<int, Recommendation>();

        // 1. Рекомендации на основе лайков и избранных пользователя
        await GenerateRecommendationsFromUserPreferencesAsync(userId, recommendationsDict);

        // 2. Коллаборативная фильтрация (похожие пользователи)
        await GenerateCollaborativeRecommendationsAsync(userId, userLikedPlaceIds, userFavoritePlaceIds, recommendationsDict);

        // 3. Новые места/мероприятия
        await GenerateNewPlacesRecommendationsAsync(userId, recommendationsDict);

        // 4. Если у пользователя есть опрос, добавляем рекомендации на его основе
        var hasCompletedSurvey = await _surveyService.HasUserCompletedSurveyAsync(userId);
        if (hasCompletedSurvey)
        {
            await GenerateRecommendationsFromSurveyAsync(userId, recommendationsDict);
        }

        // 5. Если рекомендаций мало, добавляем дефолтные
        if (recommendationsDict.Count < 9)
        {
            await GenerateDefaultRecommendationsAsync(userId, recommendationsDict);
        }

        // Добавляем бонус для мест, которые пользователь лайкнул или добавил в избранное
        foreach (var recommendation in recommendationsDict.Values)
        {
            var isLiked = userLikedPlaceIds.Contains(recommendation.PlaceId);
            var isFavorite = userFavoritePlaceIds.Contains(recommendation.PlaceId);
            
            // Большой бонус за лайк или избранное - это означает, что место точно нравится пользователю
            if (isLiked && isFavorite)
            {
                recommendation.Score += 50m; // Максимальный бонус за оба действия
            }
            else if (isLiked)
            {
                recommendation.Score += 30m; // Бонус за лайк
            }
            else if (isFavorite)
            {
                recommendation.Score += 25m; // Бонус за избранное
            }
        }

        // Получаем количество лайков для каждого места и добавляем бонус к score
        var placeIds = recommendationsDict.Keys.ToList();
        var likesCounts = await _context.Likes
            .Where(l => placeIds.Contains(l.PlaceId))
            .GroupBy(l => l.PlaceId)
            .Select(g => new { PlaceId = g.Key, LikesCount = g.Count() })
            .ToDictionaryAsync(x => x.PlaceId, x => x.LikesCount);

        // Добавляем бонус к score на основе количества лайков от всех пользователей
        foreach (var recommendation in recommendationsDict.Values)
        {
            var likesCount = likesCounts.GetValueOrDefault(recommendation.PlaceId, 0);
            // Бонус: до 10 лайков = +1 за лайк, от 10 до 50 = +0.5 за лайк, свыше 50 = фиксированный бонус
            var likesBonus = likesCount <= 10 ? likesCount * 1m : 
                            likesCount <= 50 ? 10m + (likesCount - 10) * 0.5m : 
                            30m + Math.Min((likesCount - 50) * 0.2m, 20m); // Максимум 50 бонусных очков
            recommendation.Score += likesBonus;
        }

        // Сортируем по score, затем по количеству лайков, затем по дате создания
        var finalRecommendations = recommendationsDict.Values
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => likesCounts.GetValueOrDefault(r.PlaceId, 0))
            .ThenByDescending(r => r.CreatedAt)
            .Take(9)
            .ToList();

        _context.Recommendations.AddRange(finalRecommendations);
        await _context.SaveChangesAsync();
    }

    // 1. Рекомендации на основе лайков и избранных пользователя
    private async Task GenerateRecommendationsFromUserPreferencesAsync(
        int userId, 
        Dictionary<int, Recommendation> recommendationsDict)
    {
        // Получаем все места, которые пользователь лайкнул или добавил в избранное
        var userLikedPlaces = await _context.Likes
            .Where(l => l.UserId == userId)
            .Include(l => l.Place)
                .ThenInclude(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
            .Select(l => l.Place)
            .ToListAsync();

        var userFavoritePlaces = await _context.Favorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Place)
                .ThenInclude(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
            .Select(f => f.Place)
            .ToListAsync();

        var allUserPreferredPlaces = userLikedPlaces.Union(userFavoritePlaces).ToList();

        if (!allUserPreferredPlaces.Any()) return;

        // Собираем категории из понравившихся мест
        var preferredCategoryIds = allUserPreferredPlaces
            .SelectMany(p => p.PlaceCategories.Select(pc => pc.CategoryId))
            .Distinct()
            .ToList();

        if (!preferredCategoryIds.Any()) return;

        // Находим места с такими же категориями (включая уже лайкнутые - они получат бонус)
        var similarPlaces = await _context.PlaceCategories
            .Where(pc => preferredCategoryIds.Contains(pc.CategoryId))
            .Select(pc => pc.Place)
            .Distinct()
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .ToListAsync();

        foreach (var place in similarPlaces)
        {
            var placeCategoryIds = place.PlaceCategories.Select(pc => pc.CategoryId).ToList();
            var matchingCategoriesCount = placeCategoryIds.Intersect(preferredCategoryIds).Count();
            
            // Чем больше совпадающих категорий, тем выше score
            // Бонус за количество совпадающих категорий
            var baseScore = (matchingCategoriesCount / (decimal)Math.Max(preferredCategoryIds.Count, 1)) * 100;
            var bonus = matchingCategoriesCount > 0 ? (matchingCategoriesCount * 10m) : 0m; // Бонус за каждую совпадающую категорию
            var score = Math.Min(baseScore + bonus, 100m); // Ограничиваем максимум 100

            if (!recommendationsDict.ContainsKey(place.Id) || recommendationsDict[place.Id].Score < score)
            {
                var categoryNames = string.Join(", ", place.PlaceCategories.Select(pc => pc.Category.Name).Take(3));
                recommendationsDict[place.Id] = new Recommendation
                {
                    UserId = userId,
                    PlaceId = place.Id,
                    Score = score,
                    Reason = $"Похоже на ваши избранные места. Категории: {categoryNames}",
                    CreatedAt = DateTime.UtcNow,
                    IsViewed = false
                };
            }
        }
    }

    // 2. Коллаборативная фильтрация - находим похожих пользователей
    private async Task GenerateCollaborativeRecommendationsAsync(
        int userId, 
        HashSet<int> userLikedPlaceIds,
        HashSet<int> userFavoritePlaceIds,
        Dictionary<int, Recommendation> recommendationsDict)
    {
        var userPreferredPlaceIds = userLikedPlaceIds.Union(userFavoritePlaceIds).ToHashSet();

        if (!userPreferredPlaceIds.Any()) return;

        // Находим пользователей, которые лайкнули или добавили в избранное те же места
        var similarUsers = await _context.Likes
            .Where(l => userPreferredPlaceIds.Contains(l.PlaceId) && l.UserId != userId)
            .Select(l => l.UserId)
            .Union(
                _context.Favorites
                    .Where(f => userPreferredPlaceIds.Contains(f.PlaceId) && f.UserId != userId)
                    .Select(f => f.UserId)
            )
            .GroupBy(u => u)
            .Select(g => new { UserId = g.Key, CommonPlacesCount = g.Count() })
            .OrderByDescending(x => x.CommonPlacesCount)
            .Take(10) // Топ-10 похожих пользователей
            .Select(x => x.UserId)
            .ToListAsync();

        if (!similarUsers.Any()) return;

        // Находим места, которые понравились похожим пользователям (включая уже лайкнутые пользователем)
        var collaborativePlaces = await _context.Likes
            .Where(l => similarUsers.Contains(l.UserId))
            .Select(l => new { PlaceId = l.PlaceId, UserId = l.UserId })
            .Union(
                _context.Favorites
                    .Where(f => similarUsers.Contains(f.UserId))
                    .Select(f => new { PlaceId = f.PlaceId, UserId = f.UserId })
            )
            .GroupBy(x => x.PlaceId)
            .Select(g => new { PlaceId = g.Key, Popularity = g.Count() })
            .OrderByDescending(x => x.Popularity)
            .Take(20) // Топ-20 мест от похожих пользователей
            .Select(x => x.PlaceId)
            .ToListAsync();

        if (!collaborativePlaces.Any()) return;

        // Получаем полную информацию о местах
        var places = await _context.Places
            .Where(p => collaborativePlaces.Contains(p.Id))
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .ToListAsync();

        foreach (var place in places)
        {
            var popularity = await _context.Likes
                .Where(l => l.PlaceId == place.Id && similarUsers.Contains(l.UserId))
                .CountAsync() +
                await _context.Favorites
                    .Where(f => f.PlaceId == place.Id && similarUsers.Contains(f.UserId))
                    .CountAsync();

            // Score на основе популярности среди похожих пользователей
            var score = Math.Min(80m + (popularity * 5m), 95m);

            if (!recommendationsDict.ContainsKey(place.Id) || recommendationsDict[place.Id].Score < score)
            {
                recommendationsDict[place.Id] = new Recommendation
                {
                    UserId = userId,
                    PlaceId = place.Id,
                    Score = score,
                    Reason = $"Понравилось пользователям с похожими предпочтениями",
                    CreatedAt = DateTime.UtcNow,
                    IsViewed = false
                };
            }
        }
    }

    // 3. Новые места/мероприятия
    private async Task GenerateNewPlacesRecommendationsAsync(
        int userId, 
        Dictionary<int, Recommendation> recommendationsDict)
    {
        // Места, созданные за последние 30 дней (включая уже лайкнутые)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var newPlaces = await _context.Places
            .Where(p => p.CreatedAt >= thirtyDaysAgo)
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .OrderByDescending(p => p.CreatedAt)
            .Take(15) // Топ-15 новых мест
            .ToListAsync();

        foreach (var place in newPlaces)
        {
            // Чем новее место, тем выше score
            var daysSinceCreation = (DateTime.UtcNow - place.CreatedAt).Days;
            var freshnessBonus = Math.Max(0, 30 - daysSinceCreation); // Бонус за новизну
            var baseScore = 60m + (freshnessBonus * 1.5m); // От 60 до 105

            if (!recommendationsDict.ContainsKey(place.Id) || recommendationsDict[place.Id].Score < baseScore)
            {
                var categoryNames = string.Join(", ", place.PlaceCategories.Select(pc => pc.Category.Name).Take(2));
                recommendationsDict[place.Id] = new Recommendation
                {
                    UserId = userId,
                    PlaceId = place.Id,
                    Score = baseScore,
                    Reason = $"Новое мероприятие! {categoryNames}",
                    CreatedAt = DateTime.UtcNow,
                    IsViewed = false
                };
            }
        }
    }

    // 4. Рекомендации на основе опроса (обновленная версия)
    private async Task GenerateRecommendationsFromSurveyAsync(
        int userId, 
        Dictionary<int, Recommendation> recommendationsDict)
    {
        // Получаем ответы пользователя на опрос
        var userResponses = await _context.UserSurveyResponses
            .Where(usr => usr.UserId == userId)
            .Include(usr => usr.Answer)
            .ToListAsync();

        if (!userResponses.Any()) return;

        // Вычисляем средний вес ответов пользователя
        var averageWeight = (decimal)userResponses.Average(ur => ur.Answer.Weight);

        // Определяем подходящие категории на основе веса
        var suitableCategoryTypes = DetermineCategoryTypesByWeight(averageWeight);

        // Получаем все категории подходящих типов
        var suitableCategories = await _context.Categories
            .Where(c => suitableCategoryTypes.Contains(c.Type))
            .Select(c => c.Id)
            .ToListAsync();

        if (!suitableCategories.Any()) return;

        // Получаем места, которые относятся к подходящим категориям (включая уже лайкнутые)
        var recommendedPlaces = await _context.PlaceCategories
            .Where(pc => suitableCategories.Contains(pc.CategoryId))
            .Select(pc => pc.Place)
            .Distinct()
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .ToListAsync();

        foreach (var place in recommendedPlaces)
        {
            var placeCategories = place.PlaceCategories.Select(pc => pc.Category).ToList();
            var matchingCategories = placeCategories
                .Where(c => suitableCategories.Contains(c.Id))
                .Count();

            // Оценка на основе количества совпадающих категорий и среднего веса
            var score = CalculateRecommendationScore(matchingCategories, averageWeight, placeCategories.Count);

            if (!recommendationsDict.ContainsKey(place.Id) || recommendationsDict[place.Id].Score < score)
            {
                recommendationsDict[place.Id] = new Recommendation
                {
                    UserId = userId,
                    PlaceId = place.Id,
                    Score = score,
                    Reason = GenerateRecommendationReason(placeCategories, averageWeight),
                    CreatedAt = DateTime.UtcNow,
                    IsViewed = false
                };
            }
        }
    }

    // 5. Дефолтные рекомендации для новых пользователей
    private async Task GenerateDefaultRecommendationsAsync(
        int userId, 
        Dictionary<int, Recommendation> recommendationsDict)
    {
        // Получаем все категории
        var categories = await _context.Categories.ToListAsync();
        
        // Группируем по типам
        var categoriesByType = categories.GroupBy(c => c.Type).ToList();

        // Для каждой категории берем несколько мест (включая уже лайкнутые)
        foreach (var categoryGroup in categoriesByType)
        {
            var placesInCategory = await _context.PlaceCategories
                .Where(pc => categoryGroup.Select(c => c.Id).Contains(pc.CategoryId))
                .Select(pc => pc.Place)
                .Distinct()
                .Take(3) // По 3 места из каждой категории
                .ToListAsync();

            foreach (var place in placesInCategory)
            {
                if (!recommendationsDict.ContainsKey(place.Id))
                {
                    recommendationsDict[place.Id] = new Recommendation
                    {
                        UserId = userId,
                        PlaceId = place.Id,
                        Score = 50, // Базовая оценка для новых пользователей
                        Reason = $"Рекомендация из категории: {categoryGroup.First().Name}",
                        CreatedAt = DateTime.UtcNow,
                        IsViewed = false
                    };
                }
            }
        }
    }

    private List<CategoryType> DetermineCategoryTypesByWeight(decimal averageWeight)
    {
        var types = new List<CategoryType>();

        // Определяем тип активности на основе веса
        if (averageWeight >= 10 && averageWeight <= 40)
        {
            // Активный отдых
            types.Add(CategoryType.ActivityType);
        }
        else if (averageWeight >= 50 && averageWeight <= 90)
        {
            // Спокойный отдых
            types.Add(CategoryType.ActivityType);
        }

        // Также добавляем категории по типу компании (можно расширить логику)
        types.Add(CategoryType.CompanionType);

        return types.Distinct().ToList();
    }

    private decimal CalculateRecommendationScore(int matchingCategories, decimal averageWeight, int totalCategories)
    {
        // Базовый score на основе совпадения категорий
        var categoryScore = (matchingCategories / (decimal)Math.Max(totalCategories, 1)) * 100;

        // Корректировка на основе веса ответов
        var weightScore = averageWeight;

        // Комбинированная оценка
        return (categoryScore * 0.6m) + (weightScore * 0.4m);
    }

    private string GenerateRecommendationReason(List<Category> placeCategories, decimal averageWeight)
    {
        var categoryNames = string.Join(", ", placeCategories.Select(c => c.Name));
        
        string activityType;
        if (averageWeight >= 10 && averageWeight <= 40)
        {
            activityType = "активный отдых";
        }
        else if (averageWeight >= 50 && averageWeight <= 90)
        {
            activityType = "спокойный отдых";
        }
        else
        {
            activityType = "разнообразный отдых";
        }

        return $"Подходит для {activityType}. Категории: {categoryNames}";
    }

    public async Task MarkRecommendationAsViewedAsync(int userId, int recommendationId)
    {
        var recommendation = await _context.Recommendations
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.UserId == userId);

        if (recommendation != null)
        {
            recommendation.IsViewed = true;
            await _context.SaveChangesAsync();
        }
    }
}

