using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.API.Services;

public class SurveyService : ISurveyService
{
    private readonly AdvisioDbContext _context;

    public SurveyService(AdvisioDbContext context)
    {
        _context = context;
    }

    public async Task<SurveyDto?> GetActiveSurveyAsync()
    {
        var survey = await _context.Surveys
            .Where(s => s.IsActive)
            .Include(s => s.Questions)
                .ThenInclude(q => q.Answers)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (survey == null) return null;

        return new SurveyDto
        {
            Id = survey.Id,
            Title = survey.Title,
            Description = survey.Description,
            Questions = survey.Questions
                .OrderBy(q => q.Order)
                .Select(q => new SurveyQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Order = q.Order,
                    Answers = q.Answers.Select(a => new SurveyAnswerDto
                    {
                        Id = a.Id,
                        AnswerText = a.AnswerText,
                        Weight = a.Weight
                    }).ToList()
                }).ToList()
        };
    }

    public async Task SubmitSurveyAsync(int userId, SubmitSurveyDto submitSurveyDto)
    {
        // Удаляем старые ответы пользователя на этот опрос
        var survey = await _context.Surveys
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == submitSurveyDto.SurveyId);

        if (survey == null)
            throw new ArgumentException("Опрос не найден");

        var questionIds = survey.Questions.Select(q => q.Id).ToList();
        var existingResponses = await _context.UserSurveyResponses
            .Where(usr => usr.UserId == userId && questionIds.Contains(usr.QuestionId))
            .ToListAsync();

        _context.UserSurveyResponses.RemoveRange(existingResponses);

        // Добавляем новые ответы
        foreach (var response in submitSurveyDto.Responses)
        {
            var answer = await _context.SurveyAnswers
                .FirstOrDefaultAsync(a => a.Id == response.AnswerId && a.QuestionId == response.QuestionId);

            if (answer != null)
            {
                _context.UserSurveyResponses.Add(new UserSurveyResponse
                {
                    UserId = userId,
                    QuestionId = response.QuestionId,
                    AnswerId = response.AnswerId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasUserCompletedSurveyAsync(int userId)
    {
        var activeSurvey = await _context.Surveys
            .Where(s => s.IsActive)
            .Include(s => s.Questions)
            .FirstOrDefaultAsync();

        if (activeSurvey == null) return false;

        var responseCount = await _context.UserSurveyResponses
            .Where(usr => usr.UserId == userId && 
                   activeSurvey.Questions.Select(q => q.Id).Contains(usr.QuestionId))
            .CountAsync();

        return responseCount == activeSurvey.Questions.Count;
    }
}

