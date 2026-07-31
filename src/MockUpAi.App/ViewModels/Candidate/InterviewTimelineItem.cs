using CommunityToolkit.Mvvm.ComponentModel;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class InterviewTimelineItem : ObservableObject
{
    public string QuestionId { get; init; } = string.Empty;

    public int Number { get; init; }

    public string Topic { get; init; } = "Interview";

    public string Title { get; init; } = string.Empty;

    public bool IsFollowUp { get; init; }

    [ObservableProperty]
    private string _status = "Pending";

    [ObservableProperty]
    private string _subtitle = "Waiting";

    [ObservableProperty]
    private string _accentBrush = "#CBD5E1";

    [ObservableProperty]
    private string _badgeBackground = "#F8FAFC";

    [ObservableProperty]
    private string _badgeForeground = "#475569";

    [ObservableProperty]
    private bool _canReanswer;

    [ObservableProperty]
    private bool _isReviewTarget;
}
