namespace Advisio.API.DTOs;

public class PlaceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Type { get; set; }
    public List<string> Categories { get; set; } = new();
    public bool IsFavorite { get; set; }
    public bool IsLiked { get; set; }
    public int LikesCount { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewsCount { get; set; }
    public bool IsSubscribed { get; set; }
    public bool IsVisited { get; set; }
}

public class CreatePlaceDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Type { get; set; }
    public List<int> CategoryIds { get; set; } = new();
}

public class UpdatePlaceDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Type { get; set; }
    public List<int> CategoryIds { get; set; } = new();
    public DateTime? EventStartDate { get; set; }
    public DateTime? EventEndDate { get; set; }
}
