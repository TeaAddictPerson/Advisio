using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Advisio.API.Services;

public class PlaceService : IPlaceService
{
    private readonly AdvisioDbContext _context;
    private readonly IServiceProvider _serviceProvider;

    public PlaceService(AdvisioDbContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        _serviceProvider = serviceProvider;
    }

    public async Task<PagedResult<PlaceDto>> GetAllPlacesAsync(int? userId = null, string? searchQuery = null, int? categoryId = null, string? type = null, string? sortBy = null, int page = 1, int pageSize = 30)
    {
        try
        {
            // Валидация параметров пагинации
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 100) pageSize = 100; // Максимальный размер страницы

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

            // Фильтрация по типу мероприятия
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(p => p.Type != null && p.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
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

            // Получаем общее количество записей
            var totalCount = await query.CountAsync();
            System.Diagnostics.Debug.WriteLine($"Всего мест в запросе: {totalCount}");
            
            // Применяем пагинацию
            var skip = (page - 1) * pageSize;
            var places = await query.Skip(skip).Take(pageSize).ToListAsync();
            System.Diagnostics.Debug.WriteLine($"Взято мест: {places.Count} (страница {page}, размер {pageSize})");

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

            var items = places.Select(p => MapToDto(p, userId, favoritePlaceIds, likedPlaceIds)).ToList();
            
            System.Diagnostics.Debug.WriteLine($"Возвращено мест: {items.Count}");
            if (categoryId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"Фильтрация по категории {categoryId.Value}: найдено {items.Count} мест");
            }
            
            return new PagedResult<PlaceDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
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
            Type = createPlaceDto.Type,
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
        
        // Асинхронно регенерируем рекомендации
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await recommendationService.GenerateRecommendationsAsync(userId);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                System.Diagnostics.Debug.WriteLine($"Ошибка при регенерации рекомендаций: {ex.Message}");
            }
        });
        
        return true;
    }

    public async Task<bool> RemoveFromFavoritesAsync(int userId, int placeId)
    {
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.PlaceId == placeId);

        if (favorite == null) return false;

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync();
        
        // Асинхронно регенерируем рекомендации
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await recommendationService.GenerateRecommendationsAsync(userId);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                System.Diagnostics.Debug.WriteLine($"Ошибка при регенерации рекомендаций: {ex.Message}");
            }
        });
        
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
        
        // Асинхронно регенерируем рекомендации
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await recommendationService.GenerateRecommendationsAsync(userId);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                System.Diagnostics.Debug.WriteLine($"Ошибка при регенерации рекомендаций: {ex.Message}");
            }
        });
        
        return true;
    }

    public async Task<bool> UnlikePlaceAsync(int userId, int placeId)
    {
        var like = await _context.Likes
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PlaceId == placeId);

        if (like == null) return false;

        _context.Likes.Remove(like);
        await _context.SaveChangesAsync();
        
        // Асинхронно регенерируем рекомендации
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await recommendationService.GenerateRecommendationsAsync(userId);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем выполнение
                System.Diagnostics.Debug.WriteLine($"Ошибка при регенерации рекомендаций: {ex.Message}");
            }
        });
        
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

            // Добавляем категории к месту с проверкой на дубликаты
            foreach (var categoryId in categoryIds)
            {
                // Проверяем, существует ли уже такая связь в базе данных
                var exists = await _context.PlaceCategories
                    .AnyAsync(pc => pc.PlaceId == place.Id && pc.CategoryId == categoryId);
                
                if (!exists)
                {
                    try
                    {
                        _context.PlaceCategories.Add(new PlaceCategory
                        {
                            PlaceId = place.Id,
                            CategoryId = categoryId
                        });
                    }
                    catch (Exception ex)
                    {
                        // Игнорируем ошибки дубликатов (могут возникнуть при параллельном выполнении)
                        // Логируем только если это не ошибка дубликата
                        if (!ex.Message.Contains("PRIMARY KEY") && !ex.Message.Contains("duplicate"))
                        {
                            throw;
                        }
                    }
                }
            }

            assignedCount++;
        }

        // Сохраняем изменения с обработкой ошибок дубликатов
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // Проверяем, является ли это ошибкой дубликата ключа
            if (dbEx.InnerException != null && 
                (dbEx.InnerException.Message.Contains("PRIMARY KEY") || 
                 dbEx.InnerException.Message.Contains("duplicate") ||
                 dbEx.InnerException.Message.Contains("повторяющийся ключ")))
            {
                // Это ошибка дубликата - пытаемся сохранить по одному
                var entries = _context.ChangeTracker.Entries<PlaceCategory>()
                    .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                    .ToList();
                
                foreach (var entry in entries)
                {
                    try
                    {
                        var placeId = entry.Entity.PlaceId;
                        var categoryId = entry.Entity.CategoryId;
                        
                        // Проверяем, существует ли уже такая связь
                        var exists = await _context.PlaceCategories
                            .AnyAsync(pc => pc.PlaceId == placeId && pc.CategoryId == categoryId);
                        
                        if (exists)
                        {
                            // Удаляем из контекста, если уже существует
                            _context.Entry(entry.Entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        }
                    }
                    catch
                    {
                        // Игнорируем отдельные ошибки
                    }
                }
                
                // Пытаемся сохранить оставшиеся изменения
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Если все еще есть ошибки, просто игнорируем их
                    // Это не критично для работы приложения
                }
            }
            else
            {
                // Это другая ошибка - пробрасываем дальше
                throw;
            }
        }
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
            Type = place.Type,
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

    public async Task<PlaceDto> UpdatePlaceAsync(int id, UpdatePlaceDto updatePlaceDto)
    {
        var place = await _context.Places
            .Include(p => p.PlaceCategories)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (place == null)
        {
            throw new ArgumentException($"Место с ID {id} не найдено");
        }

        // Обновляем основные поля
        place.Name = updatePlaceDto.Name.Trim();
        place.Description = updatePlaceDto.Description ?? string.Empty;
        place.Address = updatePlaceDto.Address;
        place.ImageUrl = updatePlaceDto.ImageUrl;
        place.Latitude = updatePlaceDto.Latitude;
        place.Longitude = updatePlaceDto.Longitude;
        place.Type = updatePlaceDto.Type;
        place.UpdatedAt = DateTime.UtcNow;

        // Обновляем категории
        if (updatePlaceDto.CategoryIds != null)
        {
            // Удаляем старые категории
            var oldCategories = place.PlaceCategories.ToList();
            _context.PlaceCategories.RemoveRange(oldCategories);

            // Добавляем новые категории
            foreach (var categoryId in updatePlaceDto.CategoryIds)
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
        }

        await _context.SaveChangesAsync();

        return await GetPlaceByIdAsync(place.Id) ?? throw new Exception("Место не найдено после обновления");
    }

    public async Task<bool> DeletePlaceAsync(int id)
    {
        var place = await _context.Places
            .Include(p => p.PlaceCategories)
            .Include(p => p.Favorites)
            .Include(p => p.Likes)
            .Include(p => p.Recommendations)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (place == null)
        {
            return false;
        }

        // Удаляем все связи
        _context.PlaceCategories.RemoveRange(place.PlaceCategories);
        _context.Favorites.RemoveRange(place.Favorites);
        _context.Likes.RemoveRange(place.Likes);
        _context.Recommendations.RemoveRange(place.Recommendations);

        // Удаляем само место
        _context.Places.Remove(place);
        await _context.SaveChangesAsync();

        return true;
    }
}


