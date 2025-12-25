using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveyController : ControllerBase
{
    private readonly ISurveyService _surveyService;

    public SurveyController(ISurveyService surveyService)
    {
        _surveyService = surveyService;
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
    public async Task<ActionResult<SurveyResultDto>> SubmitSurvey([FromQuery] int userId, [FromBody] SubmitSurveyDto submitSurveyDto)
    {
        try
        {
            var result = await _surveyService.SubmitSurveyAsync(userId, submitSurveyDto);
            return Ok(result);
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
}

