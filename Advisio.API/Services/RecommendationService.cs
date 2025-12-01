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

    public async Task<List<RecommendationDto>> GetRecommendationsAsync(int userId, int count = 10)
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

        if (existingRecommendations.Any())
        {
            return existingRecommendations.Select(r => new RecommendationDto
            {
                Id = r.Id,
                Place = _placeService.GetPlaceByIdAsync(r.PlaceId, userId).Result!,
                Score = r.Score,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt,
                IsViewed = r.IsViewed
            }).ToList();
        }

        // Если рекомендаций нет, генерируем их
        await GenerateRecommendationsAsync(userId);

        // Повторно получаем рекомендации
        var newRecommendations = await _context.Recommendations
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

        return newRecommendations.Select(r => new RecommendationDto
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

        var hasCompletedSurvey = await _surveyService.HasUserCompletedSurveyAsync(userId);

        if (hasCompletedSurvey)
        {
            // Генерируем рекомендации на основе опроса
            await GenerateRecommendationsFromSurveyAsync(userId);
        }
        else
        {
            // Для новых пользователей - рекомендуем по несколько мест из каждой категории
            await GenerateDefaultRecommendationsAsync(userId);
        }

        await _context.SaveChangesAsync();
    }

    private async Task GenerateRecommendationsFromSurveyAsync(int userId)
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
        // Пример: 10-40 активный отдых, 50-90 спокойный отдых
        var suitableCategoryTypes = DetermineCategoryTypesByWeight(averageWeight);

        // Получаем все категории подходящих типов
        var suitableCategories = await _context.Categories
            .Where(c => suitableCategoryTypes.Contains(c.Type))
            .Select(c => c.Id)
            .ToListAsync();

        if (!suitableCategories.Any()) return;

        // Получаем места, которые относятся к подходящим категориям
        var recommendedPlaces = await _context.PlaceCategories
            .Where(pc => suitableCategories.Contains(pc.CategoryId))
            .Select(pc => pc.Place)
            .Distinct()
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .ToListAsync();

        // Исключаем места, которые пользователь уже лайкнул или добавил в избранное
        var userLikedPlaceIds = await _context.Likes
            .Where(l => l.UserId == userId)
            .Select(l => l.PlaceId)
            .ToListAsync();

        var userFavoritePlaceIds = await _context.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.PlaceId)
            .ToListAsync();

        var excludedPlaceIds = userLikedPlaceIds.Union(userFavoritePlaceIds).ToList();

        recommendedPlaces = recommendedPlaces
            .Where(p => !excludedPlaceIds.Contains(p.Id))
            .ToList();

        // Вычисляем оценку релевантности для каждого места
        var recommendations = new List<Recommendation>();

        foreach (var place in recommendedPlaces)
        {
            var placeCategories = place.PlaceCategories.Select(pc => pc.Category).ToList();
            var matchingCategories = placeCategories
                .Where(c => suitableCategories.Contains(c.Id))
                .Count();

            // Оценка на основе количества совпадающих категорий и среднего веса
            var score = CalculateRecommendationScore(matchingCategories, averageWeight, placeCategories.Count);

            recommendations.Add(new Recommendation
            {
                UserId = userId,
                PlaceId = place.Id,
                Score = score,
                Reason = GenerateRecommendationReason(placeCategories, averageWeight),
                CreatedAt = DateTime.UtcNow,
                IsViewed = false
            });
        }

        // Сортируем по оценке и берем топ-20
        recommendations = recommendations
            .OrderByDescending(r => r.Score)
            .Take(20)
            .ToList();

        _context.Recommendations.AddRange(recommendations);
    }

    private async Task GenerateDefaultRecommendationsAsync(int userId)
    {
        // Получаем все категории
        var categories = await _context.Categories.ToListAsync();
        
        // Группируем по типам
        var categoriesByType = categories.GroupBy(c => c.Type).ToList();

        var recommendations = new List<Recommendation>();
        var recommendedPlaceIds = new HashSet<int>();

        // Для каждой категории берем несколько мест
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
                if (!recommendedPlaceIds.Contains(place.Id))
                {
                    recommendations.Add(new Recommendation
                    {
                        UserId = userId,
                        PlaceId = place.Id,
                        Score = 50, // Базовая оценка для новых пользователей
                        Reason = $"Рекомендация из категории: {categoryGroup.First().Name}",
                        CreatedAt = DateTime.UtcNow,
                        IsViewed = false
                    });

                    recommendedPlaceIds.Add(place.Id);
                }
            }
        }

        _context.Recommendations.AddRange(recommendations);
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

