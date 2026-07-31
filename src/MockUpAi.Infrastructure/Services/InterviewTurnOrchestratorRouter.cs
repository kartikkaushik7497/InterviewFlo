using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;
using MockUpAi.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MockUpAi.Infrastructure.Services;

internal sealed class InterviewTurnOrchestratorRouter : IInterviewTurnOrchestrator
{
    private readonly OpenAiInterviewTurnOrchestrator _openAi;
    private readonly GeminiInterviewTurnOrchestrator _gemini;
    private readonly HeuristicInterviewTurnOrchestrator _heuristic;
    private readonly OpenAiSettings _openAiSettings;
    private readonly GeminiSettings _geminiSettings;

    public InterviewTurnOrchestratorRouter(
        OpenAiInterviewTurnOrchestrator openAi,
        GeminiInterviewTurnOrchestrator gemini,
        HeuristicInterviewTurnOrchestrator heuristic,
        IOptions<OpenAiSettings> openAiSettings,
        IOptions<GeminiSettings> geminiSettings)
    {
        _openAi = openAi;
        _gemini = gemini;
        _heuristic = heuristic;
        _openAiSettings = openAiSettings.Value;
        _geminiSettings = geminiSettings.Value;
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
            InterviewAiProvider.Gemini when IsGeminiConfigured() => _gemini.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
            InterviewAiProvider.OpenAi when IsOpenAiConfigured() => _openAi.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
            InterviewAiProvider.Heuristic => _heuristic.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
            _ => _heuristic.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken),
        };
    }

    private bool IsOpenAiConfigured()
    {
        return _openAiSettings.Enabled &&
               (!string.IsNullOrWhiteSpace(_openAiSettings.ApiKey) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
    }

    private bool IsGeminiConfigured()
    {
        return _geminiSettings.Enabled && !string.IsNullOrWhiteSpace(_geminiSettings.ApiKey);
    }
}
