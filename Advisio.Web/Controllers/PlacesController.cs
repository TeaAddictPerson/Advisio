using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlacesController : ControllerBase
{
    private readonly IPlaceService _placeService;

    public PlacesController(IPlaceService placeService)
    {
        _placeService = placeService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PlaceDto>>> GetAllPlaces(
        [FromQuery] int? userId = null,
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? type = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        try
        {
            // Логируем параметры запроса
            System.Diagnostics.Debug.WriteLine($"GetAllPlaces: userId={userId}, search={search}, categoryId={categoryId}, type={type}, sortBy={sortBy}, page={page}, pageSize={pageSize}");
            
            var result = await _placeService.GetAllPlacesAsync(userId, search, categoryId, type, sortBy, page, pageSize);
            
            System.Diagnostics.Debug.WriteLine($"GetAllPlaces: возвращено {result.Items.Count} мест из {result.TotalCount} (страница {result.Page} из {result.TotalPages})");
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Логируем детальную ошибку
            System.Diagnostics.Debug.WriteLine($"Ошибка в GetAllPlaces: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, new { 
                error = ex.Message, 
                details = ex.ToString(),
                innerException = ex.InnerException?.Message
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlaceDto>> GetPlace(int id, [FromQuery] int? userId = null)
    {
        var place = await _placeService.GetPlaceByIdAsync(id, userId);
        if (place == null)
            return NotFound();

        return Ok(place);
    }

    [HttpPost("{placeId}/favorites")]
    public async Task<ActionResult> AddToFavorites([FromQuery] int userId, int placeId)
    {
        var result = await _placeService.AddToFavoritesAsync(userId, placeId);
        if (!result)
            return BadRequest("Место уже в избранном или не найдено");

        return Ok(new { message = "Место добавлено в избранное" });
    }

    [HttpDelete("{placeId}/favorites")]
    public async Task<ActionResult> RemoveFromFavorites([FromQuery] int userId, int placeId)
    {
        var result = await _placeService.RemoveFromFavoritesAsync(userId, placeId);
        if (!result)
            return NotFound();

        return Ok(new { message = "Место удалено из избранного" });
    }

    [HttpPost("{placeId}/likes")]
    public async Task<ActionResult> LikePlace([FromQuery] int userId, int placeId)
    {
        var result = await _placeService.LikePlaceAsync(userId, placeId);
        if (!result)
            return BadRequest("Место уже лайкнуто или не найдено");

        return Ok(new { message = "Место лайкнуто" });
    }

    [HttpDelete("{placeId}/likes")]
    public async Task<ActionResult> UnlikePlace([FromQuery] int userId, int placeId)
    {
        var result = await _placeService.UnlikePlaceAsync(userId, placeId);
        if (!result)
            return NotFound();

        return Ok(new { message = "Лайк удален" });
    }
}

