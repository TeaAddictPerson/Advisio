namespace Advisio.API.Models;

public class Recommendation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    
    public decimal Score { get; set; } // Оценка релевантности рекомендации
    public string? Reason { get; set; } // Причина рекомендации
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsViewed { get; set; } = false;
}

