using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Core.Domain.Entities;

public sealed class InterviewSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string CandidateUserId { get; set; } = string.Empty;

    public string JobRole { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public InterviewStatus Status { get; set; } = InterviewStatus.Pending;

    public List<InterviewQuestionResult> QuestionResults { get; set; } = [];

    public double OverallScore { get; set; }

    public double RoleFitScore { get; set; }
}
