using InterviewFlo.Core.Domain.Entities;

namespace InterviewFlo.Core.Application.Dtos;

public sealed class InterviewSubmitResult
{
    public required InterviewQuestionResult Result { get; init; }

    public InterviewQuestion? NextQuestion { get; init; }

    public bool IsInterviewCompleted { get; init; }

    public double RunningScore { get; init; }

    public string EncouragementMessage { get; init; } = string.Empty;
}
