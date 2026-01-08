using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface IRecommendationService
{
    Task<List<RecommendationDto>> GetRecommendationsAsync(int userId, int count = 10);
    Task GenerateRecommendationsAsync(int userId);
    Task MarkRecommendationAsViewedAsync(int userId, int recommendationId);
}

