namespace Advisio.API.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Навигационные свойства
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<UserSurveyResponse> SurveyResponses { get; set; } = new List<UserSurveyResponse>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}

public enum UserRole
{
    User = 0,
    Admin = 1
}

