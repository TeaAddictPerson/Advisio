using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface IPlaceService
{
    Task<PagedResult<PlaceDto>> GetAllPlacesAsync(int? userId = null, string? searchQuery = null, int? categoryId = null, string? type = null, string? sortBy = null, int page = 1, int pageSize = 30);
    Task<PlaceDto?> GetPlaceByIdAsync(int id, int? userId = null);
    Task<PlaceDto> CreatePlaceAsync(CreatePlaceDto createPlaceDto);
    Task<bool> AddToFavoritesAsync(int userId, int placeId);
    Task<bool> RemoveFromFavoritesAsync(int userId, int placeId);
    Task<bool> LikePlaceAsync(int userId, int placeId);
    Task<bool> UnlikePlaceAsync(int userId, int placeId);
    Task<List<PlaceDto>> GetFavoritePlacesAsync(int userId);
    Task<List<PlaceDto>> GetLikedPlacesAsync(int userId);
    Task AssignCategoriesToPlacesWithoutCategoriesAsync();
}

