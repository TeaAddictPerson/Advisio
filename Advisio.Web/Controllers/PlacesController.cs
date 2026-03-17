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

            // Записываем использование фильтров для аналитики
            if (userId.HasValue)
            {
                try
                {
                    if (categoryId.HasValue && categoryId.Value > 0)
                        await _placeService.RecordFilterUsageAsync(userId.Value, "category", categoryId.Value.ToString());
                    if (!string.IsNullOrWhiteSpace(type))
                        await _placeService.RecordFilterUsageAsync(userId.Value, "type", type!);
                    if (!string.IsNullOrWhiteSpace(sortBy))
                        await _placeService.RecordFilterUsageAsync(userId.Value, "sort", sortBy!);
                    if (!string.IsNullOrWhiteSpace(search))
                        await _placeService.RecordFilterUsageAsync(userId.Value, "search", search!);
                }
                catch { /* ignore analytics errors */ }
            }
            
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlaceDto>> GetPlace(int id, [FromQuery] int? userId = null)
    {
        try
        {
            var place = await _placeService.GetPlaceByIdAsync(id, userId);
            if (place == null)
                return NotFound();

            return Ok(place);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetPlace error: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/view")]
    public async Task<ActionResult> RecordView(int id, [FromQuery] int userId)
    {
        await _placeService.RecordPlaceViewAsync(userId, id);
        return Ok(new { message = "Просмотр записан" });
    }

    [HttpGet("{id}/reviews")]
    public async Task<ActionResult<List<PlaceReviewDto>>> GetReviews(int id)
    {
        var reviews = await _placeService.GetPlaceReviewsAsync(id);
        return Ok(reviews);
    }

    [HttpGet("{id}/comments")]
    public async Task<ActionResult<List<PlaceCommentDto>>> GetComments(int id)
    {
        var comments = await _placeService.GetPlaceCommentsAsync(id);
        return Ok(comments);
    }

    [HttpPost("{id}/reviews")]
    public async Task<ActionResult<PlaceReviewDto>> AddReview(int id, [FromQuery] int userId, [FromBody] CreateReviewDto dto)
    {
        var review = await _placeService.AddOrUpdateReviewAsync(userId, id, dto.Rating, dto.Comment);
        if (review == null) return BadRequest(new { error = "Некорректные данные" });
        return Ok(review);
    }

    [HttpPost("{id}/comments")]
    public async Task<ActionResult<PlaceCommentDto>> AddComment(int id, [FromQuery] int userId, [FromBody] CreateCommentDto dto)
    {
        var comment = await _placeService.AddPlaceCommentAsync(userId, id, dto.Comment ?? "");
        if (comment == null) return BadRequest(new { error = "Некорректные данные" });
        return Ok(comment);
    }

    [HttpPost("{id}/subscribe")]
    public async Task<ActionResult> Subscribe(int id, [FromQuery] int userId)
    {
        var result = await _placeService.SubscribeToPlaceAsync(userId, id);
        if (!result) return BadRequest("Уже подписаны или место не найдено");
        return Ok(new { message = "Подписка оформлена" });
    }

    [HttpDelete("{id}/subscribe")]
    public async Task<ActionResult> Unsubscribe(int id, [FromQuery] int userId)
    {
        var result = await _placeService.UnsubscribeFromPlaceAsync(userId, id);
        if (!result) return NotFound();
        return Ok(new { message = "Подписка отменена" });
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

    [HttpGet("favorites/{userId}")]
    public async Task<ActionResult<List<PlaceDto>>> GetFavoritePlaces(int userId)
    {
        var favorites = await _placeService.GetFavoritePlacesAsync(userId);
        return Ok(favorites);
    }

    [HttpGet("liked/{userId}")]
    public async Task<ActionResult<List<PlaceDto>>> GetLikedPlaces(int userId)
    {
        var liked = await _placeService.GetLikedPlacesAsync(userId);
        return Ok(liked);
    }

    [HttpGet("visited/{userId}")]
    public async Task<ActionResult<List<PlaceDto>>> GetVisitedPlaces(int userId)
    {
        var visited = await _placeService.GetUserVisitedPlacesAsync(userId);
        return Ok(visited);
    }

    [HttpPost("{id}/visited")]
    public async Task<ActionResult> MarkVisited(int id, [FromQuery] int userId)
    {
        var result = await _placeService.MarkPlaceVisitedAsync(userId, id);
        if (!result) return BadRequest("Место не найдено");
        return Ok(new { message = "Место отмечено как посещённое" });
    }

    [HttpDelete("{id}/visited")]
    public async Task<ActionResult> UnmarkVisited(int id, [FromQuery] int userId)
    {
        var result = await _placeService.UnmarkPlaceVisitedAsync(userId, id);
        if (!result) return NotFound();
        return Ok(new { message = "Метка о посещении снята" });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PlaceDto>> UpdatePlace(int id, [FromBody] UpdatePlaceDto dto)
    {
        try
        {
            var place = await _placeService.UpdatePlaceAsync(id, dto);
            return Ok(place);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeletePlace(int id)
    {
        var result = await _placeService.DeletePlaceAsync(id);
        if (!result)
            return NotFound("Место не найдено");

        return Ok(new { message = "Место удалено" });
    }
}

