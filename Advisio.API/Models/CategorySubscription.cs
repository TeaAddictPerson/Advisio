namespace Advisio.API.Models;

/// <summary>
/// Подписка пользователя на обновления категории
/// </summary>
public class CategorySubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}
