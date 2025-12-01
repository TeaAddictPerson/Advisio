namespace Advisio.API.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CategoryType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ICollection<PlaceCategory> PlaceCategories { get; set; } = new List<PlaceCategory>();
}

public enum CategoryType
{
    CompanionType,      // С друзьями, с ребенком, с семьей
    ActivityType        // Активный отдых, на природе, спокойный отдых
}

