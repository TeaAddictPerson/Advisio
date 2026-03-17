using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollectionsController : ControllerBase
{
    private readonly ICollectionService _collectionService;

    public CollectionsController(ICollectionService collectionService)
    {
        _collectionService = collectionService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PlaceCollectionDto>>> GetMyCollections([FromQuery] int userId)
    {
        var collections = await _collectionService.GetUserCollectionsAsync(userId);
        return Ok(collections);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PlaceCollectionDto>> GetCollection(int id, [FromQuery] int userId)
    {
        var collection = await _collectionService.GetCollectionByIdAsync(id, userId);
        if (collection == null) return NotFound();
        return Ok(collection);
    }

    [HttpPost]
    public async Task<ActionResult<PlaceCollectionDto>> CreateCollection([FromQuery] int userId, [FromBody] CreateCollectionDto dto)
    {
        var collection = await _collectionService.CreateCollectionAsync(userId, dto);
        return CreatedAtAction(nameof(GetCollection), new { id = collection.Id, userId }, collection);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PlaceCollectionDto>> UpdateCollection(int id, [FromQuery] int userId, [FromBody] UpdateCollectionDto dto)
    {
        var collection = await _collectionService.UpdateCollectionAsync(id, userId, dto);
        if (collection == null) return NotFound();
        return Ok(collection);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteCollection(int id, [FromQuery] int userId)
    {
        var result = await _collectionService.DeleteCollectionAsync(id, userId);
        if (!result) return NotFound();
        return Ok(new { message = "Коллекция удалена" });
    }

    [HttpPut("{collectionId}/places/{placeId}/note")]
    public async Task<ActionResult> UpdateItemNote(int collectionId, int placeId, [FromQuery] int userId, [FromBody] UpdateCollectionItemNoteDto? dto)
    {
        if (dto == null) return BadRequest(new { error = "Тело запроса обязательно" });
        try
        {
            var noteUrls = dto.NotePhotoUrls != null && dto.NotePhotoUrls.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(dto.NotePhotoUrls) : null;
            var result = await _collectionService.UpdateCollectionItemNoteAsync(collectionId, placeId, userId, dto.Note, noteUrls, dto.IsNotePublic);
            if (!result) return BadRequest(new { error = "Коллекция или место не найдено" });
            return Ok(new { message = "Заметка обновлена" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Ошибка сохранения: {ex.Message}" });
        }
    }

    [HttpPost("{collectionId}/places/{placeId}")]
    public async Task<ActionResult> AddPlace(int collectionId, int placeId, [FromQuery] int userId)
    {
        var result = await _collectionService.AddPlaceToCollectionAsync(collectionId, placeId, userId);
        if (!result) return BadRequest("Место уже в коллекции или не найдено");
        return Ok(new { message = "Место добавлено в коллекцию" });
    }

    [HttpDelete("{collectionId}/places/{placeId}")]
    public async Task<ActionResult> RemovePlace(int collectionId, int placeId, [FromQuery] int userId)
    {
        var result = await _collectionService.RemovePlaceFromCollectionAsync(collectionId, placeId, userId);
        if (!result) return NotFound();
        return Ok(new { message = "Место удалено из коллекции" });
    }
}
