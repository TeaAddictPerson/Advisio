using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface ISurveyService
{
    Task<SurveyDto?> GetActiveSurveyAsync();
    Task SubmitSurveyAsync(int userId, SubmitSurveyDto submitSurveyDto);
    Task<bool> HasUserCompletedSurveyAsync(int userId);
}

