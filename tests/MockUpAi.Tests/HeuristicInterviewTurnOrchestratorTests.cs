using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;
using MockUpAi.Infrastructure.Services;

namespace MockUpAi.Tests;

public sealed class HeuristicInterviewTurnOrchestratorTests
{
    [Fact]
    public async Task DecideNextTurnAsync_RewardsConcreteTechnicalBackendAnswer()
    {
        var orchestrator = new HeuristicInterviewTurnOrchestrator();
        var session = CreateBackendSession();
        var question = new InterviewQuestion
        {
            Prompt = "API: how would you design pagination and filtering for a products endpoint?",
            IdealAnswerHint = "Section:api; Expected:short; Query parameters, validation, indexes, metadata, status codes, error handling.",
        };
        const string strongAnswer =
            "I would expose page, pageSize, sort, and filter query parameters, validate limits, and return metadata like total count and next page. " +
            "For performance I would add database indexes on common filters, keep page size capped, return proper HTTP status codes, and log slow queries. " +
            "The tradeoff is offset pagination is simple, but for large tables I would use cursor pagination.";

        var decision = await orchestrator.DecideNextTurnAsync(session, question, strongAnswer, 1, 12);

        Assert.True(decision.OverallScore >= 60);
        Assert.True(decision.TechnicalScore >= 55);
        Assert.True(decision.RelevanceScore >= 35);
        Assert.False(string.IsNullOrWhiteSpace(decision.EvaluationSummary));
    }

    [Fact]
    public async Task DecideNextTurnAsync_CapsGenericLowQualityAnswer()
    {
        var orchestrator = new HeuristicInterviewTurnOrchestrator();
        var session = CreateBackendSession();
        var question = new InterviewQuestion
        {
            Prompt = "Coding: how would you count the frequency of words in a string?",
            IdealAnswerHint = "Section:coding; Expected:short; Hash map, parsing, complexity, edge cases.",
        };

        var decision = await orchestrator.DecideNextTurnAsync(session, question, "I did good work and it was nice.", 2, 12);

        Assert.True(decision.OverallScore < 55);
        Assert.True(decision.ShouldAskClarification);
        Assert.False(string.IsNullOrWhiteSpace(decision.ClarificationPrompt));
    }

    private static InterviewSession CreateBackendSession()
    {
        return new InterviewSession
        {
            CandidateUserId = "candidate_1",
            JobRole = "Backend Developer",
            JobDescription = "Build REST APIs using Node.js, MongoDB, authentication, performance optimization, and production debugging.",
            Category = InterviewCategory.Technical,
            Difficulty = InterviewDifficulty.Fresher,
            PlannedQuestionCount = 12,
            PassingScore = 60,
        };
    }
}
