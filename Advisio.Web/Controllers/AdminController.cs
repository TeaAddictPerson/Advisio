using Microsoft.AspNetCore.Mvc;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IKufarService _kufarService;
    private readonly IPlacesSyncService? _syncService;

    public AdminController(IKufarService kufarService, IPlacesSyncService? syncService = null)
    {
        _kufarService = kufarService;
        _syncService = syncService;
    }

    [HttpPost("places/load-kufar")]
    public async Task<ActionResult> LoadPlacesFromKufar()
    {
        try
        {
            await _kufarService.LoadPlacesFromKufarAsync();
            return Ok(new { message = "Места успешно загружены из Kufar.by" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("places/load-kvitki")]
    public async Task<ActionResult> LoadPlacesFromKvitki()
    {
        try
        {
            await _kufarService.LoadPlacesFromKvitkiAsync();
            return Ok(new { message = "Места успешно загружены из Kvitki.by" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("places/sync")]
    public async Task<ActionResult> SyncPlaces()
    {
        try
        {
            if (_syncService == null)
            {
                return BadRequest(new { error = "Сервис синхронизации не настроен" });
            }

            await _syncService.SyncPlacesAsync();
            return Ok(new { message = "Синхронизация мест запущена" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

