namespace Advisio.API.DTOs;

public class SurveyDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SurveyQuestionDto> Questions { get; set; } = new();
}

public class SurveyQuestionDto
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<SurveyAnswerDto> Answers { get; set; } = new();
}

public class SurveyAnswerDto
{
    public int Id { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public int Weight { get; set; }
}

public class SubmitSurveyResponseDto
{
    public int QuestionId { get; set; }
    public int AnswerId { get; set; }
}

public class SubmitSurveyDto
{
    public int SurveyId { get; set; }
    public List<SubmitSurveyResponseDto> Responses { get; set; } = new();
}

public class SurveyResultDto
{
    public bool Success { get; set; }
    public int? FavoriteCategoryId { get; set; }
    public string? FavoriteCategoryName { get; set; }
}

