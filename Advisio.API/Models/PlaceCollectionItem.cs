namespace Advisio.API.Models;

public class PlaceCollectionItem
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public PlaceCollection Collection { get; set; } = null!;
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public int SortOrder { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Заметка пользователя о планах (например, «взять билеты заранее»)</summary>
    public string? Note { get; set; }
    /// <summary>Фото (JSON-массив URL)</summary>
    public string? NotePhotoUrls { get; set; }
    /// <summary>Публичная заметка отображается в профиле</summary>
    public bool IsNotePublic { get; set; }
}
