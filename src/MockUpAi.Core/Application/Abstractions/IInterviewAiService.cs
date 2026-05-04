using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Core.Application.Abstractions;

public interface IInterviewAiService
{
    Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        string jobRole,
        string jobDescription,
        int count,
        CancellationToken cancellationToken = default);

    Task<InterviewAnswerEvaluation> EvaluateAnswerAsync(
        string jobRole,
        InterviewQuestion question,
        string candidateTranscript,
        CancellationToken cancellationToken = default);

    Task<double> CalculateRoleFitScoreAsync(
        string jobRole,
        string jobDescription,
        IReadOnlyList<InterviewQuestionResult> results,
        CancellationToken cancellationToken = default);
}
