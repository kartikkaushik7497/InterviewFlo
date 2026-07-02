namespace InterviewFlo.Core.Domain.Entities;

public sealed record InterviewAnswerEvaluation(
    bool IsCorrect,
    double Score,
    string Feedback);
