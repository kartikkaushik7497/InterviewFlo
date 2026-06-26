using CommunityToolkit.Mvvm.ComponentModel;

namespace MockUpAi.App.ViewModels.Admin;

public sealed partial class AdminCandidateRowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isMarkedForDelete;

    public string CandidateId { get; init; } = string.Empty;

    public string JobRole { get; init; } = string.Empty;

    public string JobDescription { get; init; } = string.Empty;

    public string Category { get; init; } = "Technical";

    public string Difficulty { get; init; } = "Fresher";

    public double PassingScore { get; init; } = 60;

    public string AiProvider { get; init; } = "OpenAi";
    public string InterviewerVoiceProfile { get; init; } = "OpenAI:Nova";

    public string InterviewStatus { get; init; } = string.Empty;

    public double LatestInterviewScore { get; init; }

    public bool IsPassed { get; init; }

    public double RoleFitScore { get; init; }

    public int QuestionsAnswered { get; init; }

    public string CompletedAtDisplay { get; init; } = "Pending";

    public bool IsActive { get; init; }

    public bool IsExpired { get; init; }

    public string ExpiresAtDisplay { get; init; } = "-";

    public bool MustChangePassword { get; init; }

    public string AccountStateDisplay { get; init; } = "Active";
}
