using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class InterviewAiRouterService : IInterviewAiService
{
    private readonly OpenAiInterviewAiService _openAi;
    private readonly GeminiInterviewAiService _gemini;
    private readonly HeuristicInterviewAiService _heuristic;

    public InterviewAiRouterService(
        OpenAiInterviewAiService openAi,
        GeminiInterviewAiService gemini,
        HeuristicInterviewAiService heuristic)
    {
        _openAi = openAi;
        _gemini = gemini;
        _heuristic = heuristic;
    }

    public Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        string jobDescription,
        InterviewDifficulty difficulty,
        int count,
        CancellationToken cancellationToken = default)
    {
        return Resolve(provider).GenerateQuestionsAsync(provider, jobRole, category, jobDescription, difficulty, count, cancellationToken);
    }

    public Task<string> GenerateProfessionalIntroductionAsync(
        InterviewAiProvider provider,
        string candidateName,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        CancellationToken cancellationToken = default)
    {
        return Resolve(provider).GenerateProfessionalIntroductionAsync(provider, candidateName, jobRole, category, difficulty, cancellationToken);
    }

    public Task<string> GenerateEncouragementAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        string candidateTranscript,
        CancellationToken cancellationToken = default)
    {
        return Resolve(provider).GenerateEncouragementAsync(provider, jobRole, category, difficulty, candidateTranscript, cancellationToken);
    }

    public Task<InterviewQuestion> GenerateFollowUpQuestionAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        InterviewQuestion previousQuestion,
        string candidateTranscript,
        IReadOnlyList<InterviewConversationTurn> conversationTurns,
        CancellationToken cancellationToken = default)
    {
        return Resolve(provider).GenerateFollowUpQuestionAsync(provider, jobRole, category, difficulty, previousQuestion, candidateTranscript, conversationTurns, cancellationToken);
    }

    public Task<InterviewAnswerEvaluation> EvaluateAnswerAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewDifficulty difficulty,
        InterviewQuestion question,
        string candidateTranscript,
        CancellationToken cancellationToken = default)
    {
        return Resolve(provider).EvaluateAnswerAsync(provider, jobRole, difficulty, question, candidateTranscript, cancellationToken);
    }

    public Task<double> CalculateRoleFitScoreAsync(
        InterviewAiProvider provider,
        string jobRole,
        string jobDescription,
        InterviewDifficulty difficulty,
        IReadOnlyList<InterviewQuestionResult> results,
        CancellationToken cancellationToken = default)
    {
        return Resolve(provider).CalculateRoleFitScoreAsync(provider, jobRole, jobDescription, difficulty, results, cancellationToken);
    }

    private IInterviewAiService Resolve(InterviewAiProvider provider)
    {
        return provider switch
        {
            InterviewAiProvider.Gemini => _gemini,
            InterviewAiProvider.Heuristic => _heuristic,
            _ => _openAi,
        };
    }
}
