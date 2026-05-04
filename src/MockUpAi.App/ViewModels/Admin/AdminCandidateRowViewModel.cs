namespace MockUpAi.App.ViewModels.Admin;

public sealed class AdminCandidateRowViewModel
{
    public string CandidateId { get; init; } = string.Empty;

    public string JobRole { get; init; } = string.Empty;

    public string InterviewStatus { get; init; } = string.Empty;

    public double LatestInterviewScore { get; init; }

    public double RoleFitScore { get; init; }

    public int QuestionsAnswered { get; init; }

    public string CompletedAtDisplay { get; init; } = "Pending";

    public bool IsActive { get; init; }

    public bool IsExpired { get; init; }

    public string ExpiresAtDisplay { get; init; } = "-";

    public bool MustChangePassword { get; init; }

    public string AccountStateDisplay { get; init; } = "Active";
}
