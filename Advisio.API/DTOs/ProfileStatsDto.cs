namespace Advisio.API.DTOs;

public class ProfileStatsDto
{
    public List<MostViewedPlaceDto> MostViewedPlaces { get; set; } = new();
    public List<TopFilterDto> TopFilters { get; set; } = new();
    public List<PlaceDto> Subscriptions { get; set; } = new();
    public List<PlaceDto> VisitedPlaces { get; set; } = new();
    public List<TopCategoryDto> TopCategories { get; set; } = new();
    /// <summary>Любимые типы отдыха (Place.Type) по количеству просмотров</summary>
    public List<TopPlaceTypeDto> TopPlaceTypes { get; set; } = new();
    /// <summary>Активность по дням (просмотры за последние 30 дней)</summary>
    public List<ActivityByDateDto> ActivityByDate { get; set; } = new();
    /// <summary>Публичные заметки из коллекций (для галереи в профиле)</summary>
    public List<PublicNoteDto> PublicNotes { get; set; } = new();
}

public class PublicNoteDto
{
    public int CollectionItemId { get; set; }
    public int PlaceId { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public List<string> PhotoUrls { get; set; } = new();
}

public class ActivityByDateDto
{
    public string Date { get; set; } = string.Empty;
    public int Views { get; set; }
}

public class TopPlaceTypeDto
{
    public string TypeName { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int PlacesCount { get; set; }
}

public class TopCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public string? Type { get; set; }
    public int VisitCount { get; set; }
    public int PlacesCount { get; set; }
}

public class MostViewedPlaceDto
{
    public PlaceDto Place { get; set; } = null!;
    public int ViewCount { get; set; }
}

public class TopFilterDto
{
    public string FilterType { get; set; } = string.Empty;
    public string FilterValue { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}
