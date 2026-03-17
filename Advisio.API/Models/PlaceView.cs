namespace Advisio.API.Models;

/// <summary>
/// Просмотры мест пользователем — для аналитики и статистики
/// </summary>
public class PlaceView
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}
