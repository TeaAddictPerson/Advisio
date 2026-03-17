namespace Advisio.API.DTOs;

public class PlaceCollectionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PlaceDto> Places { get; set; } = new();
    /// <summary>Детали элементов (место + заметка) для страницы коллекции</summary>
    public List<PlaceCollectionItemDto> Items { get; set; } = new();
}

public class PlaceCollectionItemDto
{
    public PlaceDto Place { get; set; } = null!;
    public string? Note { get; set; }
    public List<string> NotePhotoUrls { get; set; } = new();
    public bool IsNotePublic { get; set; }
}

public class CreateCollectionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateCollectionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateCollectionItemNoteDto
{
    public string? Note { get; set; }
    public List<string>? NotePhotoUrls { get; set; }
    public bool IsNotePublic { get; set; }
}
