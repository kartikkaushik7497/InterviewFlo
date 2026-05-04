using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateResultViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    private string _candidateId = string.Empty;

    [ObservableProperty]
    private string _jobRole = string.Empty;

    [ObservableProperty]
    private double _interviewScore;

    [ObservableProperty]
    private double _roleFitScore;

    [ObservableProperty]
    private string _verdict = string.Empty;

    [ObservableProperty]
    private string _completedAt = string.Empty;

    [ObservableProperty]
    private int _questionsAnswered;

    public CandidateResultViewModel(IAppNavigator navigator)
    {
        _navigator = navigator;
    }

    public void LoadFromSession(SessionContext sessionContext)
    {
        var session = sessionContext.LastInterview;
        var candidate = sessionContext.CurrentUser;

        if (session is null || candidate is null)
        {
            CandidateId = "Unknown";
            JobRole = "Unknown";
            Verdict = "No interview data found.";
            CompletedAt = "-";
            InterviewScore = 0;
            RoleFitScore = 0;
            QuestionsAnswered = 0;
            return;
        }

        CandidateId = candidate.UserId;
        JobRole = session.JobRole;
        InterviewScore = session.OverallScore;
        RoleFitScore = session.RoleFitScore;
        QuestionsAnswered = session.QuestionResults.Count;
        CompletedAt = session.CompletedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
        Verdict = BuildVerdict(session);
    }

    [RelayCommand]
    private async Task DoneAsync()
    {
        await _navigator.LogoutAsync();
    }

    private static string BuildVerdict(InterviewSession session)
    {
        if (session.OverallScore >= 80)
        {
            return "Excellent performance. Strong recommendation.";
        }

        if (session.OverallScore >= 65)
        {
            return "Good performance with minor gaps.";
        }

        if (session.OverallScore >= 50)
        {
            return "Average performance. Needs targeted preparation.";
        }

        return "Below benchmark. Recommend reskilling and retry.";
    }
}
