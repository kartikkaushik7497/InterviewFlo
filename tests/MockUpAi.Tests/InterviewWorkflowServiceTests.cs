using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;
using MockUpAi.Infrastructure.Persistence;
using MockUpAi.Infrastructure.Services;

namespace MockUpAi.Tests;

public sealed class InterviewWorkflowServiceTests
{
    [Fact]
    public async Task SubmitAnswerAsync_InsertsAdaptiveFollowUpBeforeNextSeedQuestion()
    {
        var workflow = new InterviewWorkflowService(
            new HeuristicInterviewAiService(),
            new AlwaysClarifyTurnOrchestrator(),
            new InMemoryInterviewRepository());

        await workflow.StartInterviewAsync(CreateCandidate());
        var firstQuestion = workflow.GetCurrentQuestion();

        var firstSubmission = await workflow.SubmitAnswerAsync("I worked on a project.");

        Assert.True(firstSubmission.IsAdaptiveFollowUp);
        Assert.NotNull(firstSubmission.NextQuestion);
        Assert.StartsWith("Follow-up:", firstSubmission.NextQuestion!.Prompt);
        Assert.Contains("Section:followup", firstSubmission.NextQuestion.IdealAnswerHint, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(firstSubmission.NextQuestion.Prompt, workflow.GetCurrentQuestion()?.Prompt);
        Assert.NotEqual(firstQuestion?.Prompt, firstSubmission.NextQuestion.Prompt);

        var followUpSubmission = await workflow.SubmitAnswerAsync("I used indexes, API validation, logging, and measured the result.");

        Assert.False(followUpSubmission.IsAdaptiveFollowUp);
        Assert.NotNull(followUpSubmission.NextQuestion);
        Assert.False(followUpSubmission.NextQuestion!.Prompt.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase));
    }

    private static AppUser CreateCandidate()
    {
        return new AppUser
        {
            UserId = "candidate_1",
            JobRole = "Backend Developer",
            JobDescription = "Build REST APIs, MongoDB queries, authentication, and production debugging.",
            InterviewCategory = InterviewCategory.Technical,
            InterviewDifficulty = InterviewDifficulty.Fresher,
            AiProvider = InterviewAiProvider.Heuristic,
            PassingScore = 60,
        };
    }

    private sealed class AlwaysClarifyTurnOrchestrator : IInterviewTurnOrchestrator
    {
        public Task<InterviewTurnDecision> DecideNextTurnAsync(
            InterviewAiProvider provider,
            InterviewSession session,
            InterviewQuestion currentQuestion,
            string candidateAnswer,
            int currentQuestionIndex,
            int totalQuestionCount,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InterviewTurnDecision
            {
                Acknowledgement = "I need one more specific detail before moving on.",
                ClarificationPrompt = "which API or database decision did you make, and what result did it create?",
                NextQuestion = "What backend testing would you add before release?",
                IdealAnswerHint = "Section:api; Expected:short; API design, database decision, result.",
                Topic = "api",
                ShouldAskClarification = true,
                ShouldMoveToNewTopic = false,
                TechnicalScore = 35,
                CommunicationScore = 55,
                DepthScore = 30,
                RelevanceScore = 45,
                ProblemSolvingScore = 40,
                OverallScore = 42,
                EvaluationSummary = "Needs more detail.",
                ObservedStrengths = ["some relevant context"],
                ObservedGaps = ["missing technical decision"],
            });
        }
    }
}
