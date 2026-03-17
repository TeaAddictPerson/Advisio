namespace Advisio.API.Models;

/// <summary>
/// Подписка пользователя на обновления конкретного места
/// </summary>
public class PlaceSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Когда пользователь последний раз просматривал место — для индикатора обновлений</summary>
    public DateTime? LastViewedAt { get; set; }
}
