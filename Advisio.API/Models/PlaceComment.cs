namespace Advisio.API.Models;

/// <summary>
/// Комментарии пользователей к местам (можно добавлять неограниченное количество)
/// </summary>
public class PlaceComment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
