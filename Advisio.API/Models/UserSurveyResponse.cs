namespace Advisio.API.Models;

public class UserSurveyResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int QuestionId { get; set; }
    public SurveyQuestion Question { get; set; } = null!;
    
    public int AnswerId { get; set; }
    public SurveyAnswer Answer { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

