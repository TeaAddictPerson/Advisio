namespace Advisio.API.Models;

public class PlaceCategory
{
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}

