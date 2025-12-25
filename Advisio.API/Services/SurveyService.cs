using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public class SurveyService : ISurveyService
{
    private readonly AdvisioDbContext _context;

    public SurveyService(AdvisioDbContext context)
    {
        _context = context;
    }

    public async Task<SurveyDto?> GetActiveSurveyAsync()
    {
        var survey = await _context.Surveys
            .Where(s => s.IsActive)
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answers)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (survey == null) return null;

        return new SurveyDto
        {
            Id = survey.Id,
            Title = survey.Title,
            Description = survey.Description,
            Questions = survey.Questions
                .OrderBy(q => q.Order)
                .Select(q => new SurveyQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Order = q.Order,
                    Answers = q.Answers.Select(a => new SurveyAnswerDto
                    {
                        Id = a.Id,
                        AnswerText = a.AnswerText,
                        Weight = a.Weight
                    }).ToList()
                }).ToList()
        };
    }

    public async Task<SurveyResultDto> SubmitSurveyAsync(int userId, SubmitSurveyDto submitSurveyDto)
    {
        // Удаляем старые ответы пользователя на этот опрос
        var survey = await _context.Surveys
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == submitSurveyDto.SurveyId);

        if (survey == null)
            throw new ArgumentException("Опрос не найден");

        var questionIds = survey.Questions.Select(q => q.Id).ToList();
        var existingResponses = await _context.UserSurveyResponses
            .Where(usr => usr.UserId == userId && questionIds.Contains(usr.QuestionId))
            .ToListAsync();

        _context.UserSurveyResponses.RemoveRange(existingResponses);

        // Добавляем новые ответы
        foreach (var response in submitSurveyDto.Responses)
        {
            var answer = await _context.SurveyAnswers
                .FirstOrDefaultAsync(a => a.Id == response.AnswerId && a.QuestionId == response.QuestionId);

            if (answer != null)
            {
                _context.UserSurveyResponses.Add(new UserSurveyResponse
                {
                    UserId = userId,
                    QuestionId = response.QuestionId,
                    AnswerId = response.AnswerId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        // Вычисляем любимую категорию
        var favoriteCategoryId = await CalculateFavoriteCategoryAsync(userId);
        var favoriteCategoryName = favoriteCategoryId.HasValue
            ? (await _context.Categories.FirstOrDefaultAsync(c => c.Id == favoriteCategoryId.Value))?.Name
            : null;

        return new SurveyResultDto
        {
            Success = true,
            FavoriteCategoryId = favoriteCategoryId,
            FavoriteCategoryName = favoriteCategoryName
        };
    }

    private async Task<int?> CalculateFavoriteCategoryAsync(int userId)
    {
        // Получаем все ответы пользователя
        var userResponses = await _context.UserSurveyResponses
            .Where(usr => usr.UserId == userId)
            .Include(usr => usr.Answer)
            .ToListAsync();

        if (!userResponses.Any())
            return null;

        // Получаем все категории
        var categories = await _context.Categories.ToListAsync();

        // Словарь для накопления весов по категориям
        var categoryWeights = new Dictionary<int, decimal>();

        foreach (var response in userResponses)
        {
            var answerText = response.Answer.AnswerText.ToLower();
            var weight = response.Answer.Weight;

            // Анализируем текст ответа и определяем категории
            foreach (var category in categories)
            {
                var categoryName = category.Name.ToLower();
                var matches = 0;

                // Проверяем ключевые слова для каждой категории
                if (categoryName.Contains("друзья") || categoryName.Contains("друг"))
                {
                    if (answerText.Contains("друг") || answerText.Contains("компани") || 
                        answerText.Contains("вечеринк") || answerText.Contains("вместе"))
                        matches++;
                }
                else if (categoryName.Contains("семья") || categoryName.Contains("семейн"))
                {
                    if (answerText.Contains("семья") || answerText.Contains("семейн") || 
                        answerText.Contains("вместе") || answerText.Contains("близк"))
                        matches++;
                }
                else if (categoryName.Contains("ребенок") || categoryName.Contains("дет"))
                {
                    if (answerText.Contains("ребенок") || answerText.Contains("дет") || 
                        answerText.Contains("малыш") || answerText.Contains("семейн"))
                        matches++;
                }
                else if (categoryName.Contains("природа") || categoryName.Contains("природ"))
                {
                    if (answerText.Contains("природа") || answerText.Contains("лес") || 
                        answerText.Contains("парк") || answerText.Contains("озеро") ||
                        answerText.Contains("река") || answerText.Contains("пикник"))
                        matches++;
                }
                else if (categoryName.Contains("активн") || categoryName.Contains("актив"))
                {
                    if (answerText.Contains("активн") || answerText.Contains("спорт") || 
                        answerText.Contains("тренировк") || answerText.Contains("поход") ||
                        answerText.Contains("пробежк") || answerText.Contains("велосипед"))
                        matches++;
                }
                else if (categoryName.Contains("спокойн") || categoryName.Contains("спокой"))
                {
                    if (answerText.Contains("спокойн") || answerText.Contains("релакс") || 
                        answerText.Contains("отдых") || answerText.Contains("тишина") ||
                        answerText.Contains("медитац") || answerText.Contains("массаж"))
                        matches++;
                }
                else if (categoryName.Contains("ресторан") || categoryName.Contains("кафе"))
                {
                    if (answerText.Contains("ресторан") || answerText.Contains("кафе") || 
                        answerText.Contains("бар") || answerText.Contains("еда") ||
                        answerText.Contains("ужин") || answerText.Contains("кухн"))
                        matches++;
                }
                else if (categoryName.Contains("концерт") || categoryName.Contains("музык"))
                {
                    if (answerText.Contains("концерт") || answerText.Contains("музык") || 
                        answerText.Contains("концерт") || answerText.Contains("джаз"))
                        matches++;
                }
                else if (categoryName.Contains("парк"))
                {
                    if (answerText.Contains("парк") || answerText.Contains("аттракцион") || 
                        answerText.Contains("прогулк") || answerText.Contains("алле"))
                        matches++;
                }
                else if (categoryName.Contains("образование") || categoryName.Contains("обучен"))
                {
                    if (answerText.Contains("образование") || answerText.Contains("лекция") || 
                        answerText.Contains("мастер-класс") || answerText.Contains("музей") ||
                        answerText.Contains("выставка") || answerText.Contains("обучен"))
                        matches++;
                }
                else if (categoryName.Contains("турнир") || categoryName.Contains("соревнован"))
                {
                    if (answerText.Contains("турнир") || answerText.Contains("соревнован") || 
                        answerText.Contains("чемпионат") || answerText.Contains("конкурс"))
                        matches++;
                }
                else if (categoryName.Contains("спектакль") || categoryName.Contains("театр"))
                {
                    if (answerText.Contains("спектакль") || answerText.Contains("театр") || 
                        answerText.Contains("постановк"))
                        matches++;
                }
                else if (categoryName.Contains("кино") || categoryName.Contains("фильм"))
                {
                    if (answerText.Contains("кино") || answerText.Contains("фильм") || 
                        answerText.Contains("cinema") || answerText.Contains("movie"))
                        matches++;
                }
                else if (categoryName.Contains("выставка") || categoryName.Contains("галерея"))
                {
                    if (answerText.Contains("выставка") || answerText.Contains("галерея") || 
                        answerText.Contains("exhibition") || answerText.Contains("gallery"))
                        matches++;
                }
                else if (categoryName.Contains("фестиваль"))
                {
                    if (answerText.Contains("фестиваль") || answerText.Contains("festival"))
                        matches++;
                }

                if (matches > 0)
                {
                    if (!categoryWeights.ContainsKey(category.Id))
                        categoryWeights[category.Id] = 0;
                    
                    // Добавляем вес ответа умноженный на количество совпадений
                    categoryWeights[category.Id] += weight * matches;
                }
            }
        }

        // Если не нашли совпадений по ключевым словам, используем средний вес
        if (!categoryWeights.Any())
        {
            var averageWeight = userResponses.Average(ur => ur.Answer.Weight);
            
            // Определяем категорию на основе среднего веса
            if (averageWeight >= 10 && averageWeight <= 40)
            {
                // Активный отдых
                var activeCategory = categories.FirstOrDefault(c => 
                    c.Name.Contains("Активный отдых", StringComparison.OrdinalIgnoreCase));
                if (activeCategory != null)
                    return activeCategory.Id;
            }
            else if (averageWeight >= 50 && averageWeight <= 90)
            {
                // Спокойный отдых
                var calmCategory = categories.FirstOrDefault(c => 
                    c.Name.Contains("Спокойный отдых", StringComparison.OrdinalIgnoreCase));
                if (calmCategory != null)
                    return calmCategory.Id;
            }
        }

        // Находим категорию с максимальным весом среди ActivityType (приоритет)
        var activityCategories = categories.Where(c => c.Type == CategoryType.ActivityType).ToList();
        var maxActivityWeight = activityCategories
            .Select(c => new { Category = c, Weight = categoryWeights.GetValueOrDefault(c.Id, 0) })
            .OrderByDescending(x => x.Weight)
            .FirstOrDefault();

        // Если есть категория ActivityType с весом > 0, возвращаем её
        if (maxActivityWeight != null && maxActivityWeight.Weight > 0)
        {
            return maxActivityWeight.Category.Id;
        }

        // Иначе ищем среди CompanionType
        var companionCategories = categories.Where(c => c.Type == CategoryType.CompanionType).ToList();
        var maxCompanionWeight = companionCategories
            .Select(c => new { Category = c, Weight = categoryWeights.GetValueOrDefault(c.Id, 0) })
            .OrderByDescending(x => x.Weight)
            .FirstOrDefault();

        if (maxCompanionWeight != null && maxCompanionWeight.Weight > 0)
        {
            return maxCompanionWeight.Category.Id;
        }

        // Если все веса равны 0, используем средний вес ответов для определения категории
        var avgWeight = userResponses.Average(ur => ur.Answer.Weight);
        if (avgWeight >= 10 && avgWeight <= 40)
        {
            var activeCategory = categories.FirstOrDefault(c => 
                c.Name.Contains("Активный отдых", StringComparison.OrdinalIgnoreCase));
            return activeCategory?.Id;
        }
        else if (avgWeight >= 50 && avgWeight <= 90)
        {
            var calmCategory = categories.FirstOrDefault(c => 
                c.Name.Contains("Спокойный отдых", StringComparison.OrdinalIgnoreCase));
            return calmCategory?.Id;
        }

        // По умолчанию возвращаем первую категорию "С друзьями"
        var defaultCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("С друзьями", StringComparison.OrdinalIgnoreCase));
        return defaultCategory?.Id;
    }

    public async Task<int?> GetUserFavoriteCategoryIdAsync(int userId)
    {
        return await CalculateFavoriteCategoryAsync(userId);
    }

    public async Task<bool> HasUserCompletedSurveyAsync(int userId)
    {
        var activeSurvey = await _context.Surveys
            .Where(s => s.IsActive)
            .Include(s => s.Questions)
            .FirstOrDefaultAsync();

        if (activeSurvey == null) return false;

        var responseCount = await _context.UserSurveyResponses
            .Where(usr => usr.UserId == userId && 
                   activeSurvey.Questions.Select(q => q.Id).Contains(usr.QuestionId))
            .CountAsync();

        return responseCount == activeSurvey.Questions.Count;
    }
}

