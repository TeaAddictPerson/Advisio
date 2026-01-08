namespace Advisio.API.Models;

public class Place
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Добавьте это свойство
    public string? Type { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Навигационные свойства
    public ICollection<PlaceCategory> PlaceCategories { get; set; } = new List<PlaceCategory>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}