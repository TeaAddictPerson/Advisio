using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface IPlaceService
{
    Task<PagedResult<PlaceDto>> GetAllPlacesAsync(int? userId = null, string? searchQuery = null, int? categoryId = null, string? type = null, string? sortBy = null, int page = 1, int pageSize = 30);
    Task<PlaceDto?> GetPlaceByIdAsync(int id, int? userId = null, bool includeExpiredEvents = false);
    Task<PlaceDto> CreatePlaceAsync(CreatePlaceDto createPlaceDto);
    Task<PlaceDto> UpdatePlaceAsync(int id, UpdatePlaceDto updatePlaceDto);
    Task<bool> DeletePlaceAsync(int id);
    Task<bool> AddToFavoritesAsync(int userId, int placeId);
    Task<bool> RemoveFromFavoritesAsync(int userId, int placeId);
    Task<bool> LikePlaceAsync(int userId, int placeId);
    Task<bool> UnlikePlaceAsync(int userId, int placeId);
    Task<List<PlaceDto>> GetFavoritePlacesAsync(int userId);
    Task<List<PlaceDto>> GetLikedPlacesAsync(int userId);
    Task AssignCategoriesToPlacesWithoutCategoriesAsync();
    Task RecordPlaceViewAsync(int userId, int placeId);
    Task RecordFilterUsageAsync(int userId, string filterType, string filterValue);
    Task<List<PlaceReviewDto>> GetPlaceReviewsAsync(int placeId);
    Task<PlaceReviewDto?> AddOrUpdateReviewAsync(int userId, int placeId, int rating, string? comment);
    Task<List<PlaceCommentDto>> GetPlaceCommentsAsync(int placeId);
    Task<PlaceCommentDto?> AddPlaceCommentAsync(int userId, int placeId, string comment);
    Task<bool> SubscribeToPlaceAsync(int userId, int placeId);
    Task<bool> UnsubscribeFromPlaceAsync(int userId, int placeId);
    Task<bool> MarkPlaceVisitedAsync(int userId, int placeId);
    Task<bool> UnmarkPlaceVisitedAsync(int userId, int placeId);
    Task<List<PlaceDto>> GetUserVisitedPlacesAsync(int userId);
    Task MarkSubscriptionViewedAsync(int userId, int placeId);
}

