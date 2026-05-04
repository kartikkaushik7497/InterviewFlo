using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Core.Application.Dtos;

public sealed class InterviewStartResult
{
    public required InterviewSession Session { get; init; }

    public required IReadOnlyList<InterviewQuestion> Questions { get; init; }
}
