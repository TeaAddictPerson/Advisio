namespace Advisio.API.Models;

public class SurveyQuestion
{
    public int Id { get; set; }
    public int SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    
    public string QuestionText { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
}

