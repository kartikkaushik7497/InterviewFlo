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

    [ObservableProperty]
    private double _passingScore = 60;

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
            PassingScore = 60;
            return;
        }

        CandidateId = candidate.UserId;
        JobRole = session.JobRole;
        InterviewScore = session.OverallScore;
        RoleFitScore = session.RoleFitScore;
        QuestionsAnswered = session.QuestionResults.Count;
        PassingScore = session.PassingScore;
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
        if (session.OverallScore >= session.PassingScore + 15)
        {
            return "Excellent performance. Strong recommendation.";
        }

        if (session.OverallScore >= session.PassingScore)
        {
            return "Passed. Good performance with minor gaps.";
        }

        if (session.OverallScore >= Math.Max(0, session.PassingScore - 10))
        {
            return "Average performance. Needs targeted preparation.";
        }

        return "Below benchmark. Recommend reskilling and retry.";
    }
}
