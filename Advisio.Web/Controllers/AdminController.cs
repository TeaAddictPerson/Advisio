using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Advisio.API.Services;
using Advisio.API.Data;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IPlaceService _placeService;
    private readonly IServiceProvider _serviceProvider;

    public AdminController(
        IPlaceService placeService,
        IServiceProvider serviceProvider)
    {
        _placeService = placeService;
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

    /// <summary>
    /// Автоподбор URL картинок для мест с пустым ImageUrl. Идёт пакетами, пока есть места без фото.
    /// </summary>
    /// <param name="maxPerBatch">Размер пакета за один вызов к БД (по умолчанию из PlacesSync:MaxPlacesImageBackfillPerRun или 50).</param>
    /// <param name="maxTotal">Остановка после стольких успешно обновлённых мест (0 = без лимита).</param>
    [HttpPost("backfill-place-images")]
    public async Task<ActionResult> BackfillPlaceImages(
        [FromQuery] int? maxPerBatch = null,
        [FromQuery] int maxTotal = 0)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var batch = maxPerBatch is > 0
                ? maxPerBatch.Value
                : cfg.GetValue<int>("PlacesSync:MaxPlacesImageBackfillPerRun", 50);
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            var totalUpdated = 0;
            while (maxTotal == 0 || totalUpdated < maxTotal)
            {
                var take = maxTotal > 0 ? Math.Min(batch, maxTotal - totalUpdated) : batch;
                if (take <= 0)
                    break;
                var n = await placeService.AssignImagesToPlacesWithoutImagesAsync(take);
                if (n == 0)
                    break;
                totalUpdated += n;
            }

            return Ok(new
            {
                message = $"Обновлено мест с картинками: {totalUpdated}",
                updated = totalUpdated
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка автоподбора картинок: {ex.Message}" });
        }
    }

    [HttpPost("sync-places")]
    public async Task<ActionResult> SyncPlaces()
    {
        try
        {
            // Создаем scope для синхронизации
            using var scope = _serviceProvider.CreateScope();
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            var totalBefore = await placeService.GetAllPlacesAsync();
            var countBefore = totalBefore.TotalCount;
            
            // Присваиваем категории местам без категорий
            await placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();
            
            var totalAfter = await placeService.GetAllPlacesAsync();
            var countAfter = totalAfter.TotalCount;

            return Ok(new { 
                message = $"Синхронизация завершена. Всего мест в базе: {countAfter}",
                total = countAfter
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при синхронизации: {ex.Message}" });
        }
    }

    /// <summary>Загрузка событий из PredictHQ в таблицу мест. <paramref name="force"/> — не ждать интервал с прошлого импорта.</summary>
    /// <param name="maxPlaces">Сколько новых мест максимум создать (по умолчанию 800; 0 = взять из PlacesSync:MaxPlacesPerSync).</param>
    [HttpPost("import-predicthq-events")]
    public async Task<ActionResult> ImportPredictHqEvents([FromQuery] bool force = true, [FromQuery] int maxPlaces = 800)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var importer = scope.ServiceProvider.GetService<IPredictHqEventsImportService>();
            if (importer == null)
                return StatusCode(503, new { error = "Сервис PredictHQ не зарегистрирован" });

            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var cap = maxPlaces > 0 ? maxPlaces : cfg.GetValue<int>("PlacesSync:MaxPlacesPerSync", 800);

            var added = await importer.ImportEventsAsync(forceRun: force, maxPlacesToAdd: cap, exhaustivePagination: true);
            return Ok(new
            {
                message = $"Импорт PredictHQ завершён. Добавлено новых мест: {added}",
                added,
                requested = cap
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка импорта PredictHQ: {ex.Message}" });
        }
    }

    // Отключена загрузка из Kufar
    // [HttpPost("load-kufar")]
    // public async Task<ActionResult> LoadKufarPlaces()
    // {
    //     try
    //     {
    //         await _kufarService.LoadPlacesFromKufarAsync();
    //         return Ok(new { message = "Места из Kufar.by успешно загружены" });
    //     }
    //     catch (Exception ex)
    //     {
    //         return StatusCode(500, new { error = $"Ошибка при загрузке мест из Kufar: {ex.Message}" });
    //     }
    // }


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
