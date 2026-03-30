using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public class PlaceService : IPlaceService
{
    private readonly AdvisioDbContext _context;
    private readonly IPlaceImageSearchService _placeImageSearch;

    public PlaceService(AdvisioDbContext context, IPlaceImageSearchService placeImageSearch)
    {
        _context = context;
        _placeImageSearch = placeImageSearch;
    }

    public async Task<PagedResult<PlaceDto>> GetAllPlacesAsync(int? userId = null, string? searchQuery = null, int? categoryId = null, string? type = null, string? sortBy = null, int page = 1, int pageSize = 30)
    {
        try
        {
            var query = _context.Places
                .Include(p => p.PlaceCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Likes)
                .AsQueryable();

            // Скрываем места с истёкшим временем проведения (мероприятия)
            var today = DateTime.UtcNow.Date;
            query = query.Where(p => p.EventEndDate == null || p.EventEndDate >= today);

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

            // Фильтрация по типу
            if (!string.IsNullOrWhiteSpace(type))
            {
                var typeLower = type.ToLower();
                query = query.Where(p => p.Type != null && p.Type.ToLower() == typeLower);
            }

            // Сортировка
            var sortByLower = string.IsNullOrEmpty(sortBy) ? null : sortBy.ToLower();
            query = sortByLower switch
            {
                "name" or "name-asc" => query.OrderBy(p => p.Name),
                "name_desc" or "name-desc" => query.OrderByDescending(p => p.Name),
                "likes" or "likes-desc" => query.OrderByDescending(p => p.Likes != null ? p.Likes.Count : 0),
                "likes_asc" or "likes-asc" => query.OrderBy(p => p.Likes != null ? p.Likes.Count : 0),
                "created" or "date-desc" => query.OrderByDescending(p => p.CreatedAt),
                "created_asc" or "date-asc" => query.OrderBy(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt) // По умолчанию по дате создания
            };

            var totalCount = await query.CountAsync();
            System.Diagnostics.Debug.WriteLine($"Всего мест в запросе: {totalCount}");
            
            // Пагинация
            var skip = (page - 1) * pageSize;
            var places = await query.Skip(skip).Take(pageSize).ToListAsync();
            System.Diagnostics.Debug.WriteLine($"Взято мест после пагинации: {places.Count}");

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

    public async Task<PlaceDto?> GetPlaceByIdAsync(int id, int? userId = null, bool includeExpiredEvents = false)
    {
        var today = DateTime.UtcNow.Date;
        var query = _context.Places
            .Include(p => p.PlaceCategories)
                .ThenInclude(pc => pc.Category)
            .Include(p => p.Likes)
            .Where(p => p.Id == id);
        if (!includeExpiredEvents)
            query = query.Where(p => p.EventEndDate == null || p.EventEndDate >= today);
        var place = await query.FirstOrDefaultAsync();

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

        var placeDto = MapToDto(place, userId, favoritePlaceIds, likedPlaceIds);

        // Добавляем рейтинг и подписку (в try-catch на случай если миграция не применена)
        try
        {
            var reviews = await _context.PlaceReviews.Where(r => r.PlaceId == id).ToListAsync();
            placeDto.ReviewsCount = reviews.Count;
            placeDto.AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : null;
            if (userId.HasValue)
            {
                placeDto.IsSubscribed = await _context.PlaceSubscriptions
                    .AnyAsync(s => s.UserId == userId.Value && s.PlaceId == id);
                try
                {
                    placeDto.IsVisited = await _context.PlaceVisits
                        .AnyAsync(v => v.UserId == userId.Value && v.PlaceId == id);
                }
                catch { /* PlaceVisits может отсутствовать */ }
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // Таблицы PlaceReviews/PlaceSubscriptions могут отсутствовать до применения миграции
        }

        return placeDto;
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

        var imageUrl = createPlaceDto.ImageUrl?.Trim();
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            try
            {
                imageUrl = await _placeImageSearch.PickFirstImageUrlAsync(
                    createPlaceDto.Name.Trim(), createPlaceDto.Type, createPlaceDto.Address);
            }
            catch
            {
                imageUrl = null;
            }
        }

        var place = new Place
        {
            Name = createPlaceDto.Name.Trim(),
            Description = createPlaceDto.Description ?? string.Empty,
            Address = createPlaceDto.Address,
            ImageUrl = imageUrl,
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

    public async Task<PlaceDto> UpdatePlaceAsync(int id, UpdatePlaceDto updatePlaceDto)
    {
        var place = await _context.Places
            .Include(p => p.PlaceCategories)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (place == null)
            throw new ArgumentException("Место не найдено");

        place.Name = updatePlaceDto.Name.Trim();
        place.Description = updatePlaceDto.Description ?? string.Empty;
        place.Address = updatePlaceDto.Address;
        place.ImageUrl = updatePlaceDto.ImageUrl?.Trim();
        place.Latitude = updatePlaceDto.Latitude;
        place.Longitude = updatePlaceDto.Longitude;
        place.Type = updatePlaceDto.Type;
        if (string.IsNullOrWhiteSpace(place.ImageUrl))
        {
            try
            {
                place.ImageUrl = await _placeImageSearch.PickFirstImageUrlAsync(
                    place.Name, place.Type, place.Address);
            }
            catch
            {
                place.ImageUrl = null;
            }
        }
        place.EventStartDate = updatePlaceDto.EventStartDate;
        place.EventEndDate = updatePlaceDto.EventEndDate;
        place.UpdatedAt = DateTime.UtcNow;

        // Обновляем категории
        _context.PlaceCategories.RemoveRange(place.PlaceCategories);
        if (updatePlaceDto.CategoryIds != null && updatePlaceDto.CategoryIds.Any())
        {
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
            .FirstOrDefaultAsync(p => p.Id == id);

        if (place == null)
            return false;

        // Удаляем связанные данные
        _context.PlaceCategories.RemoveRange(place.PlaceCategories);
        _context.Favorites.RemoveRange(place.Favorites);
        _context.Likes.RemoveRange(place.Likes);
        
        // Удаляем рекомендации, связанные с этим местом
        var recommendations = await _context.Recommendations
            .Where(r => r.PlaceId == id)
            .ToListAsync();
        _context.Recommendations.RemoveRange(recommendations);

        _context.Places.Remove(place);
        await _context.SaveChangesAsync();
        return true;
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

    public async Task RecordPlaceViewAsync(int userId, int placeId)
    {
        try
        {
            if (!await _context.Places.AnyAsync(p => p.Id == placeId)) return;
            if (!await _context.Users.AnyAsync(u => u.Id == userId)) return;
            _context.PlaceViews.Add(new PlaceView { UserId = userId, PlaceId = placeId, ViewedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
        }
        catch { /* PlaceViews может отсутствовать */ }
    }

    public async Task RecordFilterUsageAsync(int userId, string filterType, string filterValue)
    {
        try
        {
            if (!await _context.Users.AnyAsync(u => u.Id == userId)) return;
            if (string.IsNullOrWhiteSpace(filterType) || string.IsNullOrWhiteSpace(filterValue)) return;
            _context.UserFilterUsages.Add(new UserFilterUsage { UserId = userId, FilterType = filterType, FilterValue = filterValue, UsedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
        }
        catch { /* UserFilterUsages может отсутствовать */ }
    }

    public async Task<List<PlaceReviewDto>> GetPlaceReviewsAsync(int placeId)
    {
        var reviews = await _context.PlaceReviews
            .Where(r => r.PlaceId == placeId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return reviews.Select(r => new PlaceReviewDto
        {
            Id = r.Id,
            UserId = r.UserId,
            Username = r.User?.Username ?? "",
            PlaceId = r.PlaceId,
            Rating = r.Rating,
            Comment = r.Comment,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<PlaceReviewDto?> AddOrUpdateReviewAsync(int userId, int placeId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5) return null;
        if (!await _context.Places.AnyAsync(p => p.Id == placeId)) return null;
        if (!await _context.Users.AnyAsync(u => u.Id == userId)) return null;

        var existing = await _context.PlaceReviews
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PlaceId == placeId);

        // Один рейтинг на место — не разрешаем добавлять второй или менять существующий
        if (existing != null)
        {
            return new PlaceReviewDto
            {
                Id = existing.Id,
                UserId = existing.UserId,
                Username = existing.User?.Username ?? "",
                PlaceId = existing.PlaceId,
                Rating = existing.Rating,
                Comment = existing.Comment,
                CreatedAt = existing.CreatedAt
            };
        }

        _context.PlaceReviews.Add(new PlaceReview
        {
            UserId = userId,
            PlaceId = placeId,
            Rating = rating,
            Comment = comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var updated = await _context.PlaceReviews
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.PlaceId == placeId);
        return updated == null ? null : new PlaceReviewDto
        {
            Id = updated.Id,
            UserId = updated.UserId,
            Username = updated.User?.Username ?? "",
            PlaceId = updated.PlaceId,
            Rating = updated.Rating,
            Comment = updated.Comment,
            CreatedAt = updated.CreatedAt
        };
    }

    public async Task<List<PlaceCommentDto>> GetPlaceCommentsAsync(int placeId)
    {
        var comments = await _context.PlaceComments
            .Where(c => c.PlaceId == placeId)
            .Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return comments.Select(c => new PlaceCommentDto
        {
            Id = c.Id,
            UserId = c.UserId,
            Username = c.User?.Username ?? "",
            PlaceId = c.PlaceId,
            Comment = c.Comment,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<PlaceCommentDto?> AddPlaceCommentAsync(int userId, int placeId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return null;
        if (!await _context.Places.AnyAsync(p => p.Id == placeId)) return null;
        if (!await _context.Users.AnyAsync(u => u.Id == userId)) return null;

        var entity = new PlaceComment
        {
            UserId = userId,
            PlaceId = placeId,
            Comment = comment.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.PlaceComments.Add(entity);
        await _context.SaveChangesAsync();

        var updated = await _context.PlaceComments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == entity.Id);
        return updated == null ? null : new PlaceCommentDto
        {
            Id = updated.Id,
            UserId = updated.UserId,
            Username = updated.User?.Username ?? "",
            PlaceId = updated.PlaceId,
            Comment = updated.Comment,
            CreatedAt = updated.CreatedAt
        };
    }

    public async Task<bool> SubscribeToPlaceAsync(int userId, int placeId)
    {
        if (!await _context.Places.AnyAsync(p => p.Id == placeId)) return false;
        if (!await _context.Users.AnyAsync(u => u.Id == userId)) return false;
        if (await _context.PlaceSubscriptions.AnyAsync(s => s.UserId == userId && s.PlaceId == placeId)) return false;

        _context.PlaceSubscriptions.Add(new PlaceSubscription
        {
            UserId = userId,
            PlaceId = placeId,
            SubscribedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnsubscribeFromPlaceAsync(int userId, int placeId)
    {
        var sub = await _context.PlaceSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PlaceId == placeId);
        if (sub == null) return false;

        _context.PlaceSubscriptions.Remove(sub);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task MarkSubscriptionViewedAsync(int userId, int placeId)
    {
        try
        {
            var sub = await _context.PlaceSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.PlaceId == placeId);
            if (sub != null)
            {
                sub.LastViewedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
        catch { /* LastViewedAt может отсутствовать до применения миграции */ }
    }

    public async Task<bool> MarkPlaceVisitedAsync(int userId, int placeId)
    {
        if (!await _context.Places.AnyAsync(p => p.Id == placeId)) return false;
        if (!await _context.Users.AnyAsync(u => u.Id == userId)) return false;

        try
        {
            var existing = await _context.PlaceVisits
                .FirstOrDefaultAsync(v => v.UserId == userId && v.PlaceId == placeId);
            if (existing != null)
            {
                existing.VisitedAt = DateTime.UtcNow;
            }
            else
            {
                _context.PlaceVisits.Add(new PlaceVisit
                {
                    UserId = userId,
                    PlaceId = placeId,
                    VisitedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> UnmarkPlaceVisitedAsync(int userId, int placeId)
    {
        try
        {
            var visit = await _context.PlaceVisits
                .FirstOrDefaultAsync(v => v.UserId == userId && v.PlaceId == placeId);
            if (visit == null) return false;
            _context.PlaceVisits.Remove(visit);
            await _context.SaveChangesAsync();
            return true;
        }
        catch { return false; }
    }

    public async Task<List<PlaceDto>> GetUserVisitedPlacesAsync(int userId)
    {
        try
        {
            var visitIds = await _context.PlaceVisits
                .Where(v => v.UserId == userId)
                .OrderByDescending(v => v.VisitedAt)
                .Select(v => v.PlaceId)
                .ToListAsync();

            var result = new List<PlaceDto>();
            foreach (var pid in visitIds)
            {
                var p = await GetPlaceByIdAsync(pid, userId);
                if (p != null) result.Add(p);
            }
            return result;
        }
        catch { return new List<PlaceDto>(); }
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

    public async Task<int> AssignImagesToPlacesWithoutImagesAsync(int maxPlaces, CancellationToken cancellationToken = default)
    {
        if (maxPlaces <= 0)
            return 0;

        var places = await _context.Places
            .Where(p => string.IsNullOrWhiteSpace(p.ImageUrl))
            .OrderBy(p => p.Id)
            .Take(maxPlaces)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var place in places)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? url;
            try
            {
                url = await _placeImageSearch.PickFirstImageUrlAsync(place.Name, place.Type, place.Address, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(url))
                continue;

            place.ImageUrl = url;
            place.UpdatedAt = DateTime.UtcNow;
            updated++;
        }

        if (updated > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return updated;
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
            Type = place.Type,
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


