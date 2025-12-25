using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface ISurveyService
{
    Task<SurveyDto?> GetActiveSurveyAsync();
    Task<SurveyResultDto> SubmitSurveyAsync(int userId, SubmitSurveyDto submitSurveyDto);
    Task<bool> HasUserCompletedSurveyAsync(int userId);
    Task<int?> GetUserFavoriteCategoryIdAsync(int userId);
}

