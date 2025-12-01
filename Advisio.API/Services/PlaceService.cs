using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public class PlaceService : IPlaceService
{
    private readonly AdvisioDbContext _context;

    public PlaceService(AdvisioDbContext context)
    {
        _context = context;
    }

    public async Task<List<PlaceDto>> GetAllPlacesAsync(int? userId = null)
    {
        try
        {
            var places = await _context.Places
                .Include(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Likes)
                .ToListAsync();

            HashSet<int>? favoritePlaceIds = null;
            HashSet<int>? likedPlaceIds = null;

            if (userId.HasValue)
            {
                favoritePlaceIds = (await _context.Favorites
                    .Where(f => f.UserId == userId.Value)
                    .Select(f => f.PlaceId)
                    .ToListAsync()).ToHashSet();

                likedPlaceIds = (await _context.Likes
                    .Where(l => l.UserId == userId.Value)
                    .Select(l => l.PlaceId)
                    .ToListAsync()).ToHashSet();
            }

            return places.Select(p => MapToDto(p, userId, favoritePlaceIds, likedPlaceIds)).ToList();
        }
        catch (Exception ex)
        {
            // Логируем ошибку для отладки
            throw new Exception($"Ошибка при получении мест: {ex.Message}", ex);
        }
    }

    public async Task<PlaceDto?> GetPlaceByIdAsync(int id, int? userId = null)
    {
        var place = await _context.Places
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Likes)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (place == null) return null;

        HashSet<int>? favoritePlaceIds = null;
        HashSet<int>? likedPlaceIds = null;

        if (userId.HasValue)
        {
            favoritePlaceIds = (await _context.Favorites
                .Where(f => f.UserId == userId.Value && f.PlaceId == id)
                .Select(f => f.PlaceId)
                .ToListAsync()).ToHashSet();

            likedPlaceIds = (await _context.Likes
                .Where(l => l.UserId == userId.Value && l.PlaceId == id)
                .Select(l => l.PlaceId)
                .ToListAsync()).ToHashSet();
        }

        return MapToDto(place, userId, favoritePlaceIds, likedPlaceIds);
    }

    public async Task<PlaceDto> CreatePlaceAsync(CreatePlaceDto createPlaceDto)
    {
        var place = new Place
        {
            Name = createPlaceDto.Name,
            Description = createPlaceDto.Description,
            Address = createPlaceDto.Address,
            ImageUrl = createPlaceDto.ImageUrl,
            Latitude = createPlaceDto.Latitude,
            Longitude = createPlaceDto.Longitude,
            CreatedAt = DateTime.UtcNow
        };

        _context.Places.Add(place);
        await _context.SaveChangesAsync();

        // Добавляем категории
        if (createPlaceDto.CategoryIds.Any())
        {
            foreach (var categoryId in createPlaceDto.CategoryIds)
            {
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryId);
                if (categoryExists)
                {
                    _context.PlaceCategories.Add(new PlaceCategory
                    {
                        PlaceId = place.Id,
                        CategoryId = categoryId
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        return await GetPlaceByIdAsync(place.Id) ?? throw new Exception("Место не найдено после создания");
    }

    public async Task<bool> AddToFavoritesAsync(int userId, int placeId)
    {
        var exists = await _context.Favorites
            .AnyAsync(f => f.UserId == userId && f.PlaceId == placeId);

        if (exists) return false;

        _context.Favorites.Add(new Favorite
        {
            UserId = userId,
            PlaceId = placeId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFromFavoritesAsync(int userId, int placeId)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.PlaceId == placeId);

        if (favorite == null) return false;

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> LikePlaceAsync(int userId, int placeId)
    {
        var exists = await _context.Likes
            .AnyAsync(l => l.UserId == userId && l.PlaceId == placeId);

        if (exists) return false;

        _context.Likes.Add(new Like
        {
            UserId = userId,
            PlaceId = placeId,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnlikePlaceAsync(int userId, int placeId)
    {
        var like = await _context.Likes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PlaceId == placeId);

        if (like == null) return false;

        _context.Likes.Remove(like);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<PlaceDto>> GetFavoritePlacesAsync(int userId)
    {
        var favorites = await _context.Favorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Place)
                .ThenInclude(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
            .Include(f => f.Place)
                .ThenInclude(p => p.Likes)
            .Select(f => f.Place)
            .ToListAsync();

        var favoritePlaceIds = favorites.Select(p => p.Id).ToHashSet();
        var likedPlaceIds = (await _context.Likes
            .Where(l => l.UserId == userId)
            .Select(l => l.PlaceId)
            .ToListAsync()).ToHashSet();

        return favorites.Select(p => MapToDto(p, userId, favoritePlaceIds, likedPlaceIds)).ToList();
    }

    public async Task<List<PlaceDto>> GetLikedPlacesAsync(int userId)
    {
        var likes = await _context.Likes
            .Where(l => l.UserId == userId)
            .Include(l => l.Place)
                .ThenInclude(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
            .Include(l => l.Place)
                .ThenInclude(p => p.Likes)
            .Select(l => l.Place)
            .ToListAsync();

        var favoritePlaceIds = (await _context.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.PlaceId)
            .ToListAsync()).ToHashSet();
        var likedPlaceIds = likes.Select(p => p.Id).ToHashSet();

        return likes.Select(p => MapToDto(p, userId, favoritePlaceIds, likedPlaceIds)).ToList();
    }

    private PlaceDto MapToDto(Place place, int? userId, HashSet<int>? favoritePlaceIds = null, HashSet<int>? likedPlaceIds = null)
    {
        // Используем предзагруженные данные или считаем, что не избранное/не лайкнуто
        bool isFavorite = false;
        bool isLiked = false;

        if (userId.HasValue)
        {
            if (favoritePlaceIds != null)
            {
                isFavorite = favoritePlaceIds.Contains(place.Id);
            }
            // Если данные не переданы, считаем что не избранное (избегаем синхронных вызовов)

            if (likedPlaceIds != null)
            {
                isLiked = likedPlaceIds.Contains(place.Id);
            }
            // Если данные не переданы, считаем что не лайкнуто (избегаем синхронных вызовов)
        }

        return new PlaceDto
        {
            Id = place.Id,
            Name = place.Name ?? string.Empty,
            Description = place.Description ?? string.Empty,
            Address = place.Address,
            ImageUrl = place.ImageUrl,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            Categories = place.PlaceCategories?
                .Where(pc => pc.Category != null)
                .Select(pc => pc.Category!.Name)
                .ToList() ?? new List<string>(),
            IsFavorite = isFavorite,
            IsLiked = isLiked,
            LikesCount = place.Likes?.Count ?? 0
        };
    }
}

