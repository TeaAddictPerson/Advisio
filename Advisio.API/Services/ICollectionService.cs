using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface ICollectionService
{
    Task<List<PlaceCollectionDto>> GetUserCollectionsAsync(int userId);
    Task<PlaceCollectionDto?> GetCollectionByIdAsync(int id, int userId);
    Task<PlaceCollectionDto> CreateCollectionAsync(int userId, CreateCollectionDto dto);
    Task<PlaceCollectionDto?> UpdateCollectionAsync(int id, int userId, UpdateCollectionDto dto);
    Task<bool> DeleteCollectionAsync(int id, int userId);
    Task<bool> AddPlaceToCollectionAsync(int collectionId, int placeId, int userId);
    Task<bool> RemovePlaceFromCollectionAsync(int collectionId, int placeId, int userId);
    Task<bool> UpdateCollectionItemNoteAsync(int collectionId, int placeId, int userId, string? note, string? notePhotoUrls, bool isNotePublic);
}
