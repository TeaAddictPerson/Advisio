using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public class UserService : IUserService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;

    public UserService(AdvisioDbContext context, IPlaceService placeService)
    {
        _context = context;
        _placeService = placeService;
    }

    public async Task<UserProfileDto> GetUserProfileAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new ArgumentException("Пользователь не найден");

        var favorites = await _placeService.GetFavoritePlacesAsync(userId);
        var likedPlaces = await _placeService.GetLikedPlacesAsync(userId);

        return new UserProfileDto
        {
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            },
            Favorites = favorites,
            LikedPlaces = likedPlaces
        };
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<ProfileStatsDto> GetProfileStatsAsync(int userId)
    {
        var mostViewed = await _context.PlaceViews
            .Where(v => v.UserId == userId)
            .GroupBy(v => v.PlaceId)
            .Select(g => new { PlaceId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var placeIds = mostViewed.Select(x => x.PlaceId).ToList();
        var allPlaces = new List<PlaceDto>();
        foreach (var pid in placeIds)
        {
            var p = await _placeService.GetPlaceByIdAsync(pid, userId);
            if (p != null) allPlaces.Add(p);
        }

        var mostViewedPlaces = mostViewed
            .Select(mv => new { mv.PlaceId, mv.Count })
            .Join(allPlaces, mv => mv.PlaceId, p => p.Id, (mv, p) => new MostViewedPlaceDto { Place = p, ViewCount = mv.Count })
            .ToList();

        var topFilters = await _context.UserFilterUsages
            .Where(f => f.UserId == userId)
            .GroupBy(f => new { f.FilterType, f.FilterValue })
            .Select(g => new TopFilterDto
            {
                FilterType = g.Key.FilterType,
                FilterValue = g.Key.FilterValue,
                UsageCount = g.Count()
            })
            .OrderByDescending(x => x.UsageCount)
            .Take(10)
            .ToListAsync();

        // Подписки пользователя с флагом обновления (Place.UpdatedAt > LastViewedAt)
        var subscriptionsWithUpdate = new List<SubscriptionWithUpdateDto>();
        try
        {
            var subs = await _context.PlaceSubscriptions
                .Include(s => s.Place)
                    .ThenInclude(p => p!.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SubscribedAt)
                .ToListAsync();
            foreach (var sub in subs)
            {
                var p = await _placeService.GetPlaceByIdAsync(sub.PlaceId, userId, includeExpiredEvents: true);
                if (p == null && sub.Place != null)
                {
                    // Fallback: GetPlaceByIdAsync вернул null (напр. место удалено или истекло) — строим минимальный DTO из sub.Place
                    p = new PlaceDto
                    {
                        Id = sub.Place.Id,
                        Name = sub.Place.Name ?? "Без названия",
                        Description = sub.Place.Description ?? "",
                        Address = sub.Place.Address,
                        ImageUrl = sub.Place.ImageUrl,
                        Latitude = sub.Place.Latitude,
                        Longitude = sub.Place.Longitude,
                        Type = sub.Place.Type,
                        Categories = sub.Place.PlaceCategories?
                            .Where(pc => pc.Category != null)
                            .Select(pc => pc.Category!.Name ?? "")
                            .ToList() ?? new List<string>(),
                        IsFavorite = false,
                        IsLiked = false,
                        LikesCount = sub.Place.Likes?.Count ?? 0,
                    };
                }
                if (p != null)
                {
                    var placeUpdatedAt = sub.Place?.UpdatedAt;
                    // Индикатор: пользователь ещё не смотрел после подписки ИЛИ место обновлено после последнего просмотра
                    var hasUpdate = sub.LastViewedAt == null ||
                        (placeUpdatedAt != null && placeUpdatedAt > sub.LastViewedAt);
                    subscriptionsWithUpdate.Add(new SubscriptionWithUpdateDto { Place = p, HasUpdate = hasUpdate });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetProfileStats subscriptions error: {ex.Message}");
            // Fallback: простой запрос без LastViewedAt (если колонка отсутствует)
            try
            {
                var subPlaceIds = await _context.PlaceSubscriptions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SubscribedAt)
                    .Select(s => s.PlaceId)
                    .ToListAsync();
                foreach (var placeId in subPlaceIds)
                {
                    var p = await _placeService.GetPlaceByIdAsync(placeId, userId, includeExpiredEvents: true);
                    if (p != null)
                        subscriptionsWithUpdate.Add(new SubscriptionWithUpdateDto { Place = p, HasUpdate = true }); // без LastViewedAt считаем «есть обновление»
                }
            }
            catch { /* Игнорируем */ }
        }

        // Посещённые места
        var visited = await _placeService.GetUserVisitedPlacesAsync(userId);

        // Популярные категории пользователя (по просмотрам)
        var topCategories = new List<TopCategoryDto>();
        try
        {
            var viewedByCategory = await _context.PlaceViews
                .Where(v => v.UserId == userId)
                .Join(_context.PlaceCategories, v => v.PlaceId, pc => pc.PlaceId, (v, pc) => new { pc.CategoryId })
                .Join(_context.Categories, x => x.CategoryId, c => c.Id, (x, c) => new { c.Name, TypeName = c.Type.ToString() })
                .GroupBy(x => new { x.Name, x.TypeName })
                .Select(g => new { g.Key.Name, g.Key.TypeName, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            foreach (var tc in viewedByCategory)
            {
                var placesCount = await _context.PlaceCategories
                    .Where(pc => pc.Category != null && pc.Category.Name == tc.Name)
                    .CountAsync();
                topCategories.Add(new TopCategoryDto
                {
                    CategoryName = tc.Name ?? "",
                    Type = tc.TypeName,
                    VisitCount = tc.Count,
                    PlacesCount = placesCount
                });
            }
        }
        catch { /* PlaceViews может отсутствовать */ }

        // Любимые типы отдыха (Place.Type по просмотрам)
        var topPlaceTypes = new List<TopPlaceTypeDto>();
        try
        {
            var viewedByType = await _context.PlaceViews
                .Where(v => v.UserId == userId)
                .Join(_context.Places, v => v.PlaceId, p => p.Id, (v, p) => new { Type = p.Type ?? "Разное" })
                .Where(x => !string.IsNullOrEmpty(x.Type))
                .GroupBy(x => x.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            foreach (var t in viewedByType)
            {
                var placesCount = await _context.Places.CountAsync(p => (p.Type ?? "") == t.Type);
                topPlaceTypes.Add(new TopPlaceTypeDto
                {
                    TypeName = t.Type ?? "Разное",
                    ViewCount = t.Count,
                    PlacesCount = placesCount
                });
            }
        }
        catch { /* PlaceViews может отсутствовать */ }

        // Активность по дням (последние 30 дней)
        var activityByDate = new List<ActivityByDateDto>();
        try
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-30);
            var byDate = await _context.PlaceViews
                .Where(v => v.UserId == userId && v.ViewedAt >= startDate)
                .GroupBy(v => v.ViewedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            for (var d = startDate; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
            {
                var day = byDate.FirstOrDefault(x => x.Date == d);
                activityByDate.Add(new ActivityByDateDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Views = day?.Count ?? 0
                });
            }
        }
        catch { /* PlaceViews может отсутствовать */ }

        // Публичные заметки из коллекций (для галереи в профиле)
        var publicNotes = new List<PublicNoteDto>();
        try
        {
            var items = await _context.PlaceCollectionItems
                .Include(i => i.Collection)
                .Include(i => i.Place)
                .Where(i => i.Collection.UserId == userId && i.IsNotePublic)
                .Where(i => !string.IsNullOrEmpty(i.Note) || !string.IsNullOrEmpty(i.NotePhotoUrls))
                .OrderByDescending(i => i.AddedAt)
                .ToListAsync();

            foreach (var i in items)
            {
                var urls = new List<string>();
                if (!string.IsNullOrEmpty(i.NotePhotoUrls))
                {
                    try
                    {
                        urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(i.NotePhotoUrls) ?? new List<string>();
                    }
                    catch { }
                }
                publicNotes.Add(new PublicNoteDto
                {
                    CollectionItemId = i.Id,
                    PlaceId = i.PlaceId,
                    PlaceName = i.Place?.Name ?? "Место",
                    Note = i.Note,
                    PhotoUrls = urls
                });
            }
        }
        catch { /* PlaceCollectionItems может отсутствовать */ }

        return new ProfileStatsDto
        {
            MostViewedPlaces = mostViewedPlaces,
            TopFilters = topFilters,
            Subscriptions = subscriptionsWithUpdate.Select(x => x.Place).ToList(),
            SubscriptionsWithUpdate = subscriptionsWithUpdate,
            VisitedPlaces = visited,
            TopCategories = topCategories,
            TopPlaceTypes = topPlaceTypes,
            ActivityByDate = activityByDate,
            PublicNotes = publicNotes
        };
    }
}

