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
    Task<int> PruneLegacyPredictHqPlaceDuplicatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Снимок множеств для дедупликации при импорте PredictHQ (id события и логический ключ названия).</summary>
    Task<(HashSet<string> PredictHqEventIds, HashSet<string> LogicalNameKeys)> GetPredictHqImportDedupSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Все места: разбор «Дата и время: …» и строк Начало/Окончание, заполнение EventStart/End, формат как у импорта.</summary>
    Task<int> NormalizeAllPlacesEventDescriptionsAndDatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Удаляет просроченные мероприятия по EventEndDate в БД (после нормализации); если конец в БД пуст — по разбору описания.</summary>
    Task<int> DeleteExpiredEventPlacesAsync(CancellationToken cancellationToken = default);

    Task AssignCategoriesToPlacesWithoutCategoriesAsync();
    /// <summary>Автоподбор ImageUrl для мест без картинки или с ненадёжными URL (PredictHQ CDN и т.п.); лимит за вызов.</summary>
    Task<int> AssignImagesToPlacesWithoutImagesAsync(int maxPlaces, CancellationToken cancellationToken = default);
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

