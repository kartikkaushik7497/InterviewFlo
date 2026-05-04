namespace MockUpAi.Core.Application.Dtos;

public sealed class CandidateDashboardRow
{
    public string CandidateId { get; set; } = string.Empty;

    public string JobRole { get; set; } = string.Empty;

    public string InterviewStatus { get; set; } = string.Empty;

    public double LatestInterviewScore { get; set; }

    public double RoleFitScore { get; set; }

    public int QuestionsAnswered { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public bool IsActive { get; set; }

    public bool IsExpired { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
