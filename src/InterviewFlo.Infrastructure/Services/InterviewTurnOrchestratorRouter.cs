using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Application.Dtos;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class InterviewTurnOrchestratorRouter : IInterviewTurnOrchestrator
{
    private readonly OpenAiInterviewTurnOrchestrator _openAi;
    private readonly GeminiInterviewTurnOrchestrator _gemini;
    private readonly HeuristicInterviewTurnOrchestrator _heuristic;

    public InterviewTurnOrchestratorRouter(
        OpenAiInterviewTurnOrchestrator openAi,
        GeminiInterviewTurnOrchestrator gemini,
        HeuristicInterviewTurnOrchestrator heuristic)
    {
        _openAi = openAi;
        _gemini = gemini;
        _heuristic = heuristic;
    }

    public Task<InterviewTurnDecision> DecideNextTurnAsync(
        InterviewAiProvider provider,
        InterviewSession session,
        InterviewQuestion currentQuestion,
        string candidateAnswer,
        int currentQuestionIndex,
        int totalQuestionCount,
        CancellationToken cancellationToken = default)
    {
        return provider switch
        {
            InterviewAiProvider.Gemini => _gemini.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
            InterviewAiProvider.Heuristic => _heuristic.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
            _ => _openAi.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
        };
    }
}
