using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IPlaceService _placeService;
    private readonly IKufarService _kufarService;
    private readonly IServiceProvider _serviceProvider;

    public AdminController(
        IPlaceService placeService,
        IKufarService kufarService,
        IServiceProvider serviceProvider)
    {
        _placeService = placeService;
        _kufarService = kufarService;
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
            var kufarService = scope.ServiceProvider.GetRequiredService<IKufarService>();
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            // Загружаем места из Kufar
            await kufarService.LoadPlacesFromKufarAsync();
            
            // Загружаем места из Kvitki
            await kufarService.LoadPlacesFromKvitkiAsync();
            
            // Присваиваем категории местам без категорий
            await placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();

            return Ok(new { message = "Синхронизация мест завершена успешно." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при синхронизации: {ex.Message}" });
        }
    }

    [HttpPost("load-kufar")]
    public async Task<ActionResult> LoadKufarPlaces()
    {
        try
        {
            await _kufarService.LoadPlacesFromKufarAsync();
            return Ok(new { message = "Места из Kufar.by успешно загружены" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при загрузке мест из Kufar: {ex.Message}" });
        }
    }

    [HttpPost("load-kvitki")]
    public async Task<ActionResult> LoadKvitkiPlaces()
    {
        try
        {
            await _kufarService.LoadPlacesFromKvitkiAsync();
            return Ok(new { message = "Места из Kvitki.by успешно загружены" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка при загрузке мест из Kvitki: {ex.Message}" });
        }
    }
}
