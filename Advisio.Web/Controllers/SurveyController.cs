using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;
using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.Models;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveyController : ControllerBase
{
    private readonly ISurveyService _surveyService;
    private readonly AdvisioDbContext _context;

    public SurveyController(ISurveyService surveyService, AdvisioDbContext context)
    {
        _surveyService = surveyService;
        _context = context;
    }

    [HttpGet("active")]
    public async Task<ActionResult<SurveyDto>> GetActiveSurvey()
    {
        var survey = await _surveyService.GetActiveSurveyAsync();
        if (survey == null)
            return NotFound("Активный опрос не найден");

        return Ok(survey);
    }

    [HttpPost("submit")]
    public async Task<ActionResult> SubmitSurvey([FromQuery] int userId, [FromBody] SubmitSurveyDto submitSurveyDto)
    {
        try
        {
            await _surveyService.SubmitSurveyAsync(userId, submitSurveyDto);
            return Ok(new { message = "Опрос успешно отправлен" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("completed")]
    public async Task<ActionResult<bool>> HasCompletedSurvey([FromQuery] int userId)
    {
        var hasCompleted = await _surveyService.HasUserCompletedSurveyAsync(userId);
        return Ok(new { hasCompleted });
    }

    // Admin endpoints for managing questions
    [HttpDelete("questions/{questionId}")]
    public async Task<ActionResult> DeleteQuestion(int questionId)
    {
        var question = await _context.SurveyQuestions
            .Include(q => q.Answers)
            .FirstOrDefaultAsync(q => q.Id == questionId);

        if (question == null)
            return NotFound("Вопрос не найден");

        _context.SurveyAnswers.RemoveRange(question.Answers);
        _context.SurveyQuestions.Remove(question);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Вопрос удален" });
    }

    [HttpDelete("answers/{answerId}")]
    public async Task<ActionResult> DeleteAnswer(int answerId)
    {
        var answer = await _context.SurveyAnswers.FindAsync(answerId);
        if (answer == null)
            return NotFound("Ответ не найден");

        _context.SurveyAnswers.Remove(answer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Ответ удален" });
    }

    [HttpPut("questions/{questionId}")]
    public async Task<ActionResult> UpdateQuestion(int questionId, [FromBody] UpdateQuestionDto dto)
    {
        var question = await _context.SurveyQuestions.FindAsync(questionId);
        if (question == null)
            return NotFound("Вопрос не найден");

        question.QuestionText = dto.QuestionText;
        question.Order = dto.Order;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Вопрос обновлен" });
    }

    [HttpPut("answers/{answerId}")]
    public async Task<ActionResult> UpdateAnswer(int answerId, [FromBody] UpdateAnswerDto dto)
    {
        var answer = await _context.SurveyAnswers.FindAsync(answerId);
        if (answer == null)
            return NotFound("Ответ не найден");

        answer.AnswerText = dto.AnswerText;
        answer.Weight = dto.Weight;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Ответ обновлен" });
    }

    [HttpPost("questions")]
    public async Task<ActionResult> CreateQuestion([FromBody] CreateQuestionDto dto)
    {
        var survey = await _context.Surveys
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (survey == null)
            return NotFound("Активный опрос не найден");

        var question = new SurveyQuestion
        {
            SurveyId = survey.Id,
            QuestionText = dto.QuestionText,
            Order = dto.Order,
            CreatedAt = DateTime.UtcNow
        };

        _context.SurveyQuestions.Add(question);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Вопрос создан", questionId = question.Id });
    }

    [HttpPost("answers")]
    public async Task<ActionResult> CreateAnswer([FromBody] CreateAnswerDto dto)
    {
        var question = await _context.SurveyQuestions.FindAsync(dto.QuestionId);
        if (question == null)
            return NotFound("Вопрос не найден");

        var answer = new SurveyAnswer
        {
            QuestionId = dto.QuestionId,
            AnswerText = dto.AnswerText,
            Weight = dto.Weight,
            CreatedAt = DateTime.UtcNow
        };

        _context.SurveyAnswers.Add(answer);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Ответ создан", answerId = answer.Id });
    }
}

public class UpdateQuestionDto
{
    public string QuestionText { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class UpdateAnswerDto
{
    public string AnswerText { get; set; } = string.Empty;
    public int Weight { get; set; }
}

public class CreateQuestionDto
{
    public string QuestionText { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class CreateAnswerDto
{
    public int QuestionId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public int Weight { get; set; }
}
