namespace Advisio.API.Models;

/// <summary>
/// Пользовательская коллекция мест (например, "Мои планы на выходные")
/// </summary>
public class PlaceCollection
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<PlaceCollectionItem> Items { get; set; } = new List<PlaceCollectionItem>();
}
