namespace Advisio.API.DTOs;

public class RecommendationDto
{
    public int Id { get; set; }
    public PlaceDto Place { get; set; } = null!;
    public decimal Score { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsViewed { get; set; }
}

