using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;
using Advisio.API.Services;

namespace Advisio.API.Services;

public class CollectionService : ICollectionService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceService _placeService;

    public CollectionService(AdvisioDbContext context, IPlaceService placeService)
    {
        _context = context;
        _placeService = placeService;
    }

    public async Task<List<PlaceCollectionDto>> GetUserCollectionsAsync(int userId)
    {
        try
        {
            return await GetUserCollectionsInternalAsync(userId);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // Таблицы PlaceCollections могут отсутствовать до применения миграции
            return new List<PlaceCollectionDto>();
        }
    }

    private async Task<List<PlaceCollectionDto>> GetUserCollectionsInternalAsync(int userId)
    {
        var collections = await _context.PlaceCollections
            .Where(c => c.UserId == userId)
            .Include(c => c.Items)
                .ThenInclude(i => i.Place)
                    .ThenInclude(p => p.PlaceCategories)
                        .ThenInclude(pc => pc.Category)
            .Include(c => c.Items)
                .ThenInclude(i => i.Place)
                    .ThenInclude(p => p.Likes)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var favoriteIds = (await _context.Favorites.Where(f => f.UserId == userId).Select(f => f.PlaceId).ToListAsync()).ToHashSet();
        var likedIds = (await _context.Likes.Where(l => l.UserId == userId).Select(l => l.PlaceId).ToListAsync()).ToHashSet();

        return collections.Select(c => MapToDto(c, userId, favoriteIds, likedIds)).ToList();
    }

    public async Task<PlaceCollectionDto?> GetCollectionByIdAsync(int id, int userId)
    {
        var collection = await _context.PlaceCollections
            .Include(c => c.Items.OrderBy(i => i.SortOrder))
                .ThenInclude(i => i.Place)
                    .ThenInclude(p => p.PlaceCategories)
                        .ThenInclude(pc => pc.Category)
            .Include(c => c.Items)
                .ThenInclude(i => i.Place)
                    .ThenInclude(p => p.Likes)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (collection == null) return null;

        var favoriteIds = (await _context.Favorites.Where(f => f.UserId == userId).Select(f => f.PlaceId).ToListAsync()).ToHashSet();
        var likedIds = (await _context.Likes.Where(l => l.UserId == userId).Select(l => l.PlaceId).ToListAsync()).ToHashSet();

        return MapToDto(collection, userId, favoriteIds, likedIds);
    }

    public async Task<PlaceCollectionDto> CreateCollectionAsync(int userId, CreateCollectionDto dto)
    {
        var collection = new PlaceCollection
        {
            UserId = userId,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.PlaceCollections.Add(collection);
        await _context.SaveChangesAsync();
        return (await GetCollectionByIdAsync(collection.Id, userId))!;
    }

    public async Task<bool> AddPlaceToCollectionAsync(int collectionId, int placeId, int userId)
    {
        var collection = await _context.PlaceCollections.FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);
        if (collection == null) return false;
        if (!await _context.Places.AnyAsync(p => p.Id == placeId)) return false;

        var exists = await _context.PlaceCollectionItems.AnyAsync(i => i.CollectionId == collectionId && i.PlaceId == placeId);
        if (exists) return false;

        var maxOrder = await _context.PlaceCollectionItems
            .Where(i => i.CollectionId == collectionId)
            .MaxAsync(i => (int?)i.SortOrder) ?? 0;

        _context.PlaceCollectionItems.Add(new PlaceCollectionItem
        {
            CollectionId = collectionId,
            PlaceId = placeId,
            SortOrder = maxOrder + 1,
            AddedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemovePlaceFromCollectionAsync(int collectionId, int placeId, int userId)
    {
        var collection = await _context.PlaceCollections.FirstOrDefaultAsync(c => c.Id == collectionId && c.UserId == userId);
        if (collection == null) return false;

        var item = await _context.PlaceCollectionItems
            .FirstOrDefaultAsync(i => i.CollectionId == collectionId && i.PlaceId == placeId);
        if (item == null) return false;

        _context.PlaceCollectionItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    private static PlaceCollectionDto MapToDto(PlaceCollection c, int userId, HashSet<int> favoriteIds, HashSet<int> likedIds)
    {
        var places = c.Items
            .OrderBy(i => i.SortOrder)
            .Select(i => i.Place)
            .Select(p => new PlaceDto
            {
                Id = p.Id,
                Name = p.Name ?? "",
                Description = p.Description ?? "",
                Address = p.Address,
                ImageUrl = p.ImageUrl,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                Type = p.Type,
                Categories = p.PlaceCategories?.Select(pc => pc.Category?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
                IsFavorite = favoriteIds.Contains(p.Id),
                IsLiked = likedIds.Contains(p.Id),
                LikesCount = p.Likes?.Count ?? 0
            })
            .ToList();

        var items = c.Items
            .OrderBy(i => i.SortOrder)
            .Select(i =>
            {
                var p = i.Place;
                var placeDto = new PlaceDto
                {
                    Id = p.Id,
                    Name = p.Name ?? "",
                    Description = p.Description ?? "",
                    Address = p.Address,
                    ImageUrl = p.ImageUrl,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Type = p.Type,
                    Categories = p.PlaceCategories?.Select(pc => pc.Category?.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
                    IsFavorite = favoriteIds.Contains(p.Id),
                    IsLiked = likedIds.Contains(p.Id),
                    LikesCount = p.Likes?.Count ?? 0
                };
                var urls = new List<string>();
                if (!string.IsNullOrEmpty(i.NotePhotoUrls))
                {
                    try
                    {
                        urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(i.NotePhotoUrls) ?? new List<string>();
                    }
                    catch { }
                }
                return new PlaceCollectionItemDto
                {
                    Place = placeDto,
                    Note = i.Note,
                    NotePhotoUrls = urls,
                    IsNotePublic = i.IsNotePublic
                };
            })
            .ToList();

        return new PlaceCollectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            CreatedAt = c.CreatedAt,
            Places = places,
            Items = items
        };
    }
}
