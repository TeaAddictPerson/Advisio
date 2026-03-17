using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface ICollectionService
{
    Task<List<PlaceCollectionDto>> GetUserCollectionsAsync(int userId);
    Task<PlaceCollectionDto?> GetCollectionByIdAsync(int id, int userId);
    Task<PlaceCollectionDto> CreateCollectionAsync(int userId, CreateCollectionDto dto);
    Task<bool> AddPlaceToCollectionAsync(int collectionId, int placeId, int userId);
    Task<bool> RemovePlaceFromCollectionAsync(int collectionId, int placeId, int userId);
}
