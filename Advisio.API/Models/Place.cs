namespace Advisio.API.Models;

public class Place
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? Type { get; set; }

    /// <summary>Начало мероприятия (для фестивалей, выставок и т.д.)</summary>
    public DateTime? EventStartDate { get; set; }
    /// <summary>Окончание мероприятия — места с истекшей датой не показываются</summary>
    public DateTime? EventEndDate { get; set; }

    /// <summary>Уникальный id события PredictHQ (<c>id</c> в ответе API) — защита от дубликатов при импорте.</summary>
    public string? PredictHqEventId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Навигационные свойства
    public ICollection<PlaceCategory> PlaceCategories { get; set; } = new List<PlaceCategory>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Like> Likes { get; set; } = new List<Like>();
    public ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    public ICollection<PlaceCollectionItem> CollectionItems { get; set; } = new List<PlaceCollectionItem>();
    public ICollection<PlaceView> PlaceViews { get; set; } = new List<PlaceView>();
    public ICollection<PlaceReview> PlaceReviews { get; set; } = new List<PlaceReview>();
    public ICollection<PlaceSubscription> PlaceSubscriptions { get; set; } = new List<PlaceSubscription>();
    public ICollection<PlaceVisit> PlaceVisits { get; set; } = new List<PlaceVisit>();
    public ICollection<PlaceComment> PlaceComments { get; set; } = new List<PlaceComment>();
}