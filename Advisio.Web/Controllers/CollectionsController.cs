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
