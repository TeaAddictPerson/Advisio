using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationsController(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RecommendationDto>>> GetRecommendations(
        [FromQuery] int userId,
        [FromQuery] int count = 9)
    {
        var recommendations = await _recommendationService.GetRecommendationsAsync(userId, count);
        return Ok(recommendations);
    }

    [HttpPost("generate")]
    public async Task<ActionResult> GenerateRecommendations([FromQuery] int userId)
    {
        await _recommendationService.GenerateRecommendationsAsync(userId);
        return Ok(new { message = "Рекомендации сгенерированы" });
    }

    [HttpPost("{recommendationId}/viewed")]
    public async Task<ActionResult> MarkAsViewed([FromQuery] int userId, int recommendationId)
    {
        await _recommendationService.MarkRecommendationAsViewedAsync(userId, recommendationId);
        return Ok(new { message = "Рекомендация отмечена как просмотренная" });
    }
}

