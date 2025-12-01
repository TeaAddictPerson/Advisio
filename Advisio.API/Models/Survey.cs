namespace Advisio.API.Models;

public class Survey
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
}

