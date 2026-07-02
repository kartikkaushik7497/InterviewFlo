using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InterviewFlo.App.Services;
using InterviewFlo.Core.Domain.Entities;

namespace InterviewFlo.App.ViewModels.Candidate;

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

    [ObservableProperty]
    private double _technicalScore;

    [ObservableProperty]
    private double _communicationScore;

    [ObservableProperty]
    private double _depthScore;

    [ObservableProperty]
    private double _relevanceScore;

    [ObservableProperty]
    private string _improvementSummary = string.Empty;

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
            TechnicalScore = 0;
            CommunicationScore = 0;
            DepthScore = 0;
            RelevanceScore = 0;
            ImprovementSummary = string.Empty;
            return;
        }

        CandidateId = candidate.UserId;
        JobRole = session.JobRole;
        InterviewScore = session.OverallScore;
        RoleFitScore = session.RoleFitScore;
        QuestionsAnswered = session.QuestionResults.Count;
        PassingScore = session.PassingScore;
        TechnicalScore = Average(session.QuestionResults.Select(x => x.TechnicalScore));
        CommunicationScore = Average(session.QuestionResults.Select(x => x.CommunicationScore));
        DepthScore = Average(session.QuestionResults.Select(x => x.DepthScore));
        RelevanceScore = Average(session.QuestionResults.Select(x => x.RelevanceScore));
        ImprovementSummary = BuildImprovementSummary(session);
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

    private static double Average(IEnumerable<double> values)
    {
        var meaningful = values.Where(x => x > 0).ToList();
        return meaningful.Count == 0 ? 0 : Math.Round(meaningful.Average(), 2);
    }

    private static string BuildImprovementSummary(InterviewSession session)
    {
        var gaps = session.QuestionResults
            .SelectMany(x => x.Gaps.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        return gaps.Count == 0
            ? "Keep practicing with concrete examples, tradeoffs, and measurable outcomes."
            : $"Focus areas: {string.Join(", ", gaps)}.";
    }
}
