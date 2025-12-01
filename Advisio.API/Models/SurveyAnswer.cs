namespace Advisio.API.Models;

public class SurveyAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public SurveyQuestion Question { get; set; } = null!;
    
    public string AnswerText { get; set; } = string.Empty;
    public int Weight { get; set; } // Вес ответа (10-40 активный отдых, 50-90 спокойный и т.д.)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

