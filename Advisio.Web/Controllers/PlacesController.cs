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
    public async Task<ActionResult<List<PlaceDto>>> GetAllPlaces([FromQuery] int? userId = null)
    {
        var places = await _placeService.GetAllPlacesAsync(userId);
        return Ok(places);
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

