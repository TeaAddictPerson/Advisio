using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;

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
            CreatedAt = user.CreatedAt
        };
    }
}

