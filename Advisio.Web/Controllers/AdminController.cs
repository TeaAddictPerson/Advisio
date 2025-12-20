using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Advisio.API.Services;
using Advisio.API.Data;

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

    [HttpPost("clear-places-keep-first-7")]
    public async Task<ActionResult> ClearPlacesKeepFirst7()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AdvisioDbContext>();

            // Получаем ID первых 7 мест
            var first7Ids = await context.Places
                .OrderBy(p => p.Id)
                .Take(7)
                .Select(p => p.Id)
                .ToListAsync();

            if (first7Ids.Count == 0)
            {
                return Ok(new { message = "В базе нет мест для очистки" });
            }

            var totalBefore = await context.Places.CountAsync();

            // Удаляем связи с категориями
            var placeCategoriesToDelete = await context.PlaceCategories
                .Where(pc => !first7Ids.Contains(pc.PlaceId))
                .ToListAsync();
            context.PlaceCategories.RemoveRange(placeCategoriesToDelete);

            // Удаляем связи с избранным
            var favoritesToDelete = await context.Favorites
                .Where(f => !first7Ids.Contains(f.PlaceId))
                .ToListAsync();
            context.Favorites.RemoveRange(favoritesToDelete);

            // Удаляем связи с лайками
            var likesToDelete = await context.Likes
                .Where(l => !first7Ids.Contains(l.PlaceId))
                .ToListAsync();
            context.Likes.RemoveRange(likesToDelete);

            // Удаляем рекомендации
            var recommendationsToDelete = await context.Recommendations
                .Where(r => !first7Ids.Contains(r.PlaceId))
                .ToListAsync();
            context.Recommendations.RemoveRange(recommendationsToDelete);

            // Удаляем места
            var placesToDelete = await context.Places
                .Where(p => !first7Ids.Contains(p.Id))
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
}
