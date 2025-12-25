using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Advisio.API.Services;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IPlaceService _placeService;
    private readonly IPredictHQService _predictHQService;
    private readonly IServiceProvider _serviceProvider;

    public AdminController(
        IPlaceService placeService,
        IPredictHQService predictHQService,
        IServiceProvider serviceProvider)
    {
        _placeService = placeService;
        _predictHQService = predictHQService;
        _serviceProvider = serviceProvider;
    }

    [HttpPost("assign-categories")]
    public async Task<ActionResult> AssignCategoriesToPlaces()
    {
        try
        {
            await _placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();
            return Ok(new { message = "Категории успешно присвоены местам без категорий" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при присвоении категорий: {ex.Message}" });
        }
    }

    [HttpPost("sync-places")]
    public async Task<ActionResult> SyncPlaces()
    {
        try
        {
            // Создаем scope для синхронизации
            using var scope = _serviceProvider.CreateScope();
            var predictHQService = scope.ServiceProvider.GetRequiredService<IPredictHQService>();
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminController>>();

            logger.LogInformation("🔄 Начало синхронизации мест из PredictHQ...");

            var totalBefore = await placeService.GetAllPlacesAsync();
            var countBefore = totalBefore.TotalCount;
            logger.LogInformation("📊 Количество мест до синхронизации: {Count}", countBefore);
            
            // Загружаем места из PredictHQ
            logger.LogInformation("📥 Загрузка мест из PredictHQ API...");
            await predictHQService.LoadPlacesFromPredictHQAsync();
            logger.LogInformation("✅ Загрузка из PredictHQ завершена");
            
            // Присваиваем категории местам без категорий
            logger.LogInformation("🏷️ Присвоение категорий местам...");
            await placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();
            logger.LogInformation("✅ Категории присвоены");
            
            var totalAfter = await placeService.GetAllPlacesAsync();
            var countAfter = totalAfter.TotalCount;
            var added = countAfter - countBefore;

            logger.LogInformation("✅ Синхронизация завершена. Добавлено: {Added}, Всего: {Total}", added, countAfter);

            return Ok(new { 
                message = $"Синхронизация завершена. Добавлено новых мест: {added}. Всего мест в базе: {countAfter}",
                added = added,
                total = countAfter
            });
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<AdminController>>();
            logger.LogError(ex, "❌ Ошибка при синхронизации: {Error}", ex.Message);
            return StatusCode(500, new { error = $"Ошибка при синхронизации: {ex.Message}" });
        }
    }

    [HttpPost("load-predicthq")]
    public async Task<ActionResult> LoadPredictHQPlaces()
    {
        try
        {
            await _predictHQService.LoadPlacesFromPredictHQAsync();
            return Ok(new { message = "Места из PredictHQ успешно загружены" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при загрузке мест из PredictHQ: {ex.Message}" });
        }
    }

    [HttpPost("update-existing-places")]
    public async Task<ActionResult> UpdateExistingPlaces()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var predictHQService = scope.ServiceProvider.GetRequiredService<IPredictHQService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminController>>();

            logger.LogInformation("🔄 Начало обновления всех существующих мест...");
            
            await predictHQService.UpdateExistingPlacesAsync();
            
            logger.LogInformation("✅ Обновление всех существующих мест завершено");
            
            return Ok(new { message = "Все существующие места успешно обновлены" });
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<AdminController>>();
            logger.LogError(ex, "❌ Ошибка при обновлении существующих мест: {Error}", ex.Message);
            return StatusCode(500, new { error = $"Ошибка при обновлении существующих мест: {ex.Message}" });
        }
    }

    [HttpPost("clear-places-keep-first-23")]
    public async Task<ActionResult> ClearPlacesKeepFirst23()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AdvisioDbContext>();

            // Получаем ID первых 23 мест
            var first23Ids = await context.Places
                .OrderBy(p => p.Id)
                .Take(23)
                .Select(p => p.Id)
                .ToListAsync();

            if (first23Ids.Count == 0)
            {
                return Ok(new { message = "В базе нет мест для очистки" });
            }

            var totalBefore = await context.Places.CountAsync();

            // Удаляем связи с категориями
            var placeCategoriesToDelete = await context.PlaceCategories
                .Where(pc => !first23Ids.Contains(pc.PlaceId))
                .ToListAsync();
            context.PlaceCategories.RemoveRange(placeCategoriesToDelete);

            // Удаляем связи с избранным
            var favoritesToDelete = await context.Favorites
                .Where(f => !first23Ids.Contains(f.PlaceId))
                .ToListAsync();
            context.Favorites.RemoveRange(favoritesToDelete);

            // Удаляем связи с лайками
            var likesToDelete = await context.Likes
                .Where(l => !first23Ids.Contains(l.PlaceId))
                .ToListAsync();
            context.Likes.RemoveRange(likesToDelete);

            // Удаляем рекомендации
            var recommendationsToDelete = await context.Recommendations
                .Where(r => !first23Ids.Contains(r.PlaceId))
                .ToListAsync();
            context.Recommendations.RemoveRange(recommendationsToDelete);

            // Удаляем места
            var placesToDelete = await context.Places
                .Where(p => !first23Ids.Contains(p.Id))
                .ToListAsync();
            context.Places.RemoveRange(placesToDelete);

            await context.SaveChangesAsync();

            var totalAfter = await context.Places.CountAsync();
            var deleted = totalBefore - totalAfter;

            return Ok(new { 
                message = $"Очистка завершена. Удалено мест: {deleted}. Осталось мест: {totalAfter}",
                deleted = deleted,
                remaining = totalAfter
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при очистке БД: {ex.Message}" });
        }
    }

    [HttpPost("update-event-types")]
    public async Task<ActionResult> UpdateEventTypes()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var predictHQService = scope.ServiceProvider.GetRequiredService<IPredictHQService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdminController>>();

            logger.LogInformation("🔄 Начало обновления типов мероприятий для существующих мест...");
            
            await predictHQService.UpdateEventTypesForExistingPlacesAsync();
            
            logger.LogInformation("✅ Обновление типов мероприятий завершено");
            
            return Ok(new { message = "Типы мероприятий успешно обновлены для всех существующих мест" });
        }
        catch (Exception ex)
        {
            var logger = _serviceProvider.GetRequiredService<ILogger<AdminController>>();
            logger.LogError(ex, "❌ Ошибка при обновлении типов мероприятий: {Error}", ex.Message);
            return StatusCode(500, new { error = $"Ошибка при обновлении типов мероприятий: {ex.Message}" });
        }
    }

    private bool IsAdmin()
    {
        var role = HttpContext.Session.GetString("Role");
        return role == "Admin";
    }

    [HttpPost("places")]
    public async Task<ActionResult<PlaceDto>> CreatePlace([FromBody] CreatePlaceDto createPlaceDto)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new { error = "Требуются права администратора" });
        }

        try
        {
            var place = await _placeService.CreatePlaceAsync(createPlaceDto);
            return Ok(place);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при создании места: {ex.Message}" });
        }
    }

    [HttpPut("places/{id}")]
    public async Task<ActionResult<PlaceDto>> UpdatePlace(int id, [FromBody] UpdatePlaceDto updatePlaceDto)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new { error = "Требуются права администратора" });
        }

        try
        {
            var place = await _placeService.UpdatePlaceAsync(id, updatePlaceDto);
            return Ok(place);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при обновлении места: {ex.Message}" });
        }
    }

    [HttpDelete("places/{id}")]
    public async Task<ActionResult> DeletePlace(int id)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new { error = "Требуются права администратора" });
        }

        try
        {
            var deleted = await _placeService.DeletePlaceAsync(id);
            if (deleted)
            {
                return Ok(new { message = "Место успешно удалено" });
            }
            return NotFound(new { error = "Место не найдено" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при удалении места: {ex.Message}" });
        }
    }

    [HttpPost("survey/add-question")]
    public async Task<ActionResult> AddQuestion([FromBody] AddQuestionDto addQuestionDto)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new { error = "Требуются права администратора" });
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AdvisioDbContext>();
            var surveyService = scope.ServiceProvider.GetRequiredService<ISurveyService>();

            // Получаем активный опрос или создаем новый
            var activeSurvey = await context.Surveys
                .Where(s => s.IsActive)
                .FirstOrDefaultAsync();

            if (activeSurvey == null)
            {
                activeSurvey = new Survey
                {
                    Title = "Основной опрос",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Surveys.Add(activeSurvey);
                await context.SaveChangesAsync();
            }

            // Создаем вопрос
            var question = new SurveyQuestion
            {
                SurveyId = activeSurvey.Id,
                QuestionText = addQuestionDto.QuestionText,
                Order = addQuestionDto.Order,
                CreatedAt = DateTime.UtcNow
            };
            context.SurveyQuestions.Add(question);
            await context.SaveChangesAsync();

            // Создаем ответы
            foreach (var answerDto in addQuestionDto.Answers)
            {
                var answer = new SurveyAnswer
                {
                    QuestionId = question.Id,
                    AnswerText = answerDto.AnswerText,
                    Weight = answerDto.Weight,
                    CreatedAt = DateTime.UtcNow
                };
                context.SurveyAnswers.Add(answer);
            }
            await context.SaveChangesAsync();

            return Ok(new { message = "Вопрос успешно добавлен", questionId = question.Id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при добавлении вопроса: {ex.Message}" });
        }
    }

    [HttpDelete("survey/delete-question/{id}")]
    public async Task<ActionResult> DeleteQuestion(int id)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new { error = "Требуются права администратора" });
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AdvisioDbContext>();

            var question = await context.SurveyQuestions
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
            {
                return NotFound(new { error = "Вопрос не найден" });
            }

            // Удаляем ответы
            context.SurveyAnswers.RemoveRange(question.Answers);

            // Удаляем ответы пользователей на этот вопрос
            var userResponses = await context.UserSurveyResponses
                .Where(r => r.Answer.QuestionId == id)
                .ToListAsync();
            context.UserSurveyResponses.RemoveRange(userResponses);

            // Удаляем вопрос
            context.SurveyQuestions.Remove(question);
            await context.SaveChangesAsync();

            return Ok(new { message = "Вопрос успешно удален" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при удалении вопроса: {ex.Message}" });
        }
    }
}

public class AddQuestionDto
{
    public string QuestionText { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<AddAnswerDto> Answers { get; set; } = new();
}

public class AddAnswerDto
{
    public string AnswerText { get; set; } = string.Empty;
    public int Weight { get; set; }
}
