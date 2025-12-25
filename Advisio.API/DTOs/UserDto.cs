using Advisio.API.Models;

namespace Advisio.API.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; }
}

public class UserProfileDto
{
    public UserDto User { get; set; } = null!;
    public List<PlaceDto> Favorites { get; set; } = new();
    public List<PlaceDto> LikedPlaces { get; set; } = new();
}

