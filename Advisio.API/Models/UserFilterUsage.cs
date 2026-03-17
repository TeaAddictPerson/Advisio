namespace Advisio.API.Models;

/// <summary>
/// Использование фильтров пользователем — для аналитики
/// </summary>
public class UserFilterUsage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string FilterType { get; set; } = string.Empty; // "category", "type", "sort"
    public string FilterValue { get; set; } = string.Empty;
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}
