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

    public async Task<List<PlaceDto>> GetAllPlacesAsync(int? userId = null, string? searchQuery = null, int? categoryId = null, string? sortBy = null)
    {
        try
        {
            var query = _context.Places
                .Include(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Likes)
                .AsQueryable();

            // Поиск по названию или описанию
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var searchLower = searchQuery.ToLower();
                query = query.Where(p => 
                    (p.Name != null && p.Name.ToLower().Contains(searchLower)) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                    (p.Address != null && p.Address.ToLower().Contains(searchLower))
                );
            }

            // Фильтрация по категории
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.PlaceCategories != null && 
                                         p.PlaceCategories.Any(pc => pc.CategoryId == categoryId.Value));
            }

            // Сортировка
            var sortByLower = string.IsNullOrEmpty(sortBy) ? null : sortBy.ToLower();
            query = sortByLower switch
            {
                "name" => query.OrderBy(p => p.Name),
                "name_desc" => query.OrderByDescending(p => p.Name),
                "likes" => query.OrderByDescending(p => p.Likes != null ? p.Likes.Count : 0),
                "likes_asc" => query.OrderBy(p => p.Likes != null ? p.Likes.Count : 0),
                "created" => query.OrderByDescending(p => p.CreatedAt),
                "created_asc" => query.OrderBy(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt) // По умолчанию по дате создания
            };

            var totalCount = await query.CountAsync();
            System.Diagnostics.Debug.WriteLine($"Всего мест в запросе: {totalCount}");
            
            var places = await query.Take(20).ToListAsync();
            System.Diagnostics.Debug.WriteLine($"Взято мест после Take(20): {places.Count}");

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

            var result = places.Select(p => MapToDto(p, userId, favoritePlaceIds, likedPlaceIds)).ToList();
            
            System.Diagnostics.Debug.WriteLine($"Возвращено мест: {result.Count}");
            if (categoryId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"Фильтрация по категории {categoryId.Value}: найдено {result.Count} мест");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            // Логируем ошибку для отладки
            System.Diagnostics.Debug.WriteLine($"Ошибка при получении мест: {ex.Message}\n{ex.StackTrace}");
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
        // Валидация
        if (string.IsNullOrWhiteSpace(createPlaceDto.Name))
        {
            throw new ArgumentException("Название места не может быть пустым", nameof(createPlaceDto));
        }

        // Проверяем, не существует ли уже место с таким именем (case-insensitive)
        var existingPlace = await _context.Places
            .FirstOrDefaultAsync(p => p.Name != null && p.Name.ToLower() == createPlaceDto.Name.ToLower());
        
        if (existingPlace != null)
        {
            throw new InvalidOperationException($"Место с названием '{createPlaceDto.Name}' уже существует");
        }

        var place = new Place
        {
            Name = createPlaceDto.Name.Trim(),
            Description = createPlaceDto.Description ?? string.Empty,
            Address = createPlaceDto.Address,
            ImageUrl = createPlaceDto.ImageUrl,
            Latitude = createPlaceDto.Latitude,
            Longitude = createPlaceDto.Longitude,
            CreatedAt = DateTime.UtcNow
        };

        _context.Places.Add(place);
        await _context.SaveChangesAsync();

        // Добавляем категории
        if (createPlaceDto.CategoryIds != null && createPlaceDto.CategoryIds.Any())
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

    public async Task AssignCategoriesToPlacesWithoutCategoriesAsync()
    {
        try
        {
            // Получаем все места без категорий
            var placesWithoutCategories = await _context.Places
                .Include(p => p.PlaceCategories)
                .Where(p => !p.PlaceCategories.Any())
                .ToListAsync();

            if (!placesWithoutCategories.Any())
                return;

        // Получаем все категории
        var categories = await _context.Categories.ToListAsync();

        // Находим категории по ключевым словам
        var activeCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("Активный отдых", StringComparison.OrdinalIgnoreCase));
        var calmCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("Спокойный отдых", StringComparison.OrdinalIgnoreCase));
        var natureCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("На природе", StringComparison.OrdinalIgnoreCase));
        var friendsCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("С друзьями", StringComparison.OrdinalIgnoreCase));
        var familyCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("С семьей", StringComparison.OrdinalIgnoreCase));
        var childCategory = categories.FirstOrDefault(c => 
            c.Name.Contains("С ребенком", StringComparison.OrdinalIgnoreCase));

        int assignedCount = 0;

        foreach (var place in placesWithoutCategories)
        {
            var categoryIds = new List<int>();
            var text = ((place.Name ?? "") + " " + (place.Description ?? "")).ToLower();

            // Определяем категории на основе текста
            if (activeCategory != null && 
                (text.Contains("спорт") || text.Contains("фитнес") || text.Contains("актив") || 
                 text.Contains("тренировк") || text.Contains("заняти")))
            {
                categoryIds.Add(activeCategory.Id);
            }

            if (calmCategory != null && 
                (text.Contains("музей") || text.Contains("театр") || text.Contains("выставка") || 
                 text.Contains("кино") || text.Contains("концерт") || text.Contains("галерея")))
            {
                categoryIds.Add(calmCategory.Id);
            }

            if (natureCategory != null && 
                (text.Contains("парк") || text.Contains("природа") || text.Contains("лес") || 
                 text.Contains("озеро") || text.Contains("река") || text.Contains("пляж")))
            {
                categoryIds.Add(natureCategory.Id);
            }

            // Определяем категорию по типу компании
            if (familyCategory != null && 
                (text.Contains("семья") || text.Contains("семейн")))
            {
                categoryIds.Add(familyCategory.Id);
            }
            else if (childCategory != null && 
                     (text.Contains("дет") || text.Contains("ребенок") || text.Contains("малыш")))
            {
                categoryIds.Add(childCategory.Id);
            }
            else if (friendsCategory != null)
            {
                // По умолчанию - с друзьями
                categoryIds.Add(friendsCategory.Id);
            }

            // Если не определили ни одной категории, добавляем "С друзьями" по умолчанию
            if (!categoryIds.Any() && friendsCategory != null)
            {
                categoryIds.Add(friendsCategory.Id);
            }

            // Получаем существующие категории для этого места
            var existingCategoryIds = await _context.PlaceCategories
                .Where(pc => pc.PlaceId == place.Id)
                .Select(pc => pc.CategoryId)
                .ToListAsync();

            // Добавляем категории к месту
            foreach (var categoryId in categoryIds)
            {
                if (!existingCategoryIds.Contains(categoryId))
                {
                    _context.PlaceCategories.Add(new PlaceCategory
                    {
                        PlaceId = place.Id,
                        CategoryId = categoryId
                    });
                }
            }

            assignedCount++;
        }

        await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при присвоении категорий местам: {ex.Message}", ex);
        }
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

