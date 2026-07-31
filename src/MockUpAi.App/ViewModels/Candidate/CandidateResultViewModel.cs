using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.Core.Domain.Entities;
using System.Collections.ObjectModel;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateResultViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    private string _submissionTitle = "Interview Submitted Successfully";

    [ObservableProperty]
    private string _submissionMessage = "Thank you. Your interview has been submitted and saved for admin review.";

    [ObservableProperty]
    private string _candidateId = "Unknown";

    [ObservableProperty]
    private string _jobRole = "Unknown";

    [ObservableProperty]
    private string _questionsSummary = "0 answered";

    [ObservableProperty]
    private string _completedAt = "-";

    [ObservableProperty]
    private string _interviewType = "-";

    [ObservableProperty]
    private string _submissionStatus = "Submitted";

    [ObservableProperty]
    private string _adminReviewMessage = "Your detailed evaluation is available only to the admin panel.";

    public ObservableCollection<InterviewTimelineItem> TimelineItems { get; } = [];

    public CandidateResultViewModel(IAppNavigator navigator)
    {
        _navigator = navigator;
    }

    public void LoadFromSession(SessionContext sessionContext)
    {
        var session = sessionContext.LastInterview;
        var candidate = sessionContext.CurrentUser;

        CandidateId = candidate?.UserId ?? session?.CandidateUserId ?? "Unknown";
        JobRole = session?.JobRole ?? candidate?.JobRole ?? "Unknown";
        TimelineItems.Clear();

        if (session is null)
        {
            SubmissionTitle = "Interview Submission Status";
            SubmissionMessage = "No completed interview session was found, but you can safely logout and ask the admin to verify the dashboard.";
            QuestionsSummary = "No saved answers found";
            CompletedAt = "-";
            InterviewType = candidate?.InterviewCategory.ToString() ?? "-";
            SubmissionStatus = "Not available";
            AdminReviewMessage = "If you reached this page after finishing the interview, ask the admin to refresh the candidate dashboard.";
            return;
        }

        var answered = session.QuestionResults.Count;
        var planned = Math.Max(session.PlannedQuestionCount, answered);
        var completionLabel = answered >= planned
            ? "All planned questions answered"
            : "Submitted before all planned questions were answered";

        SubmissionTitle = "Interview Submitted Successfully";
        SubmissionMessage = "Thank you. Your interview has been saved and sent to the admin dashboard for review.";
        QuestionsSummary = $"{answered} of {planned} answered";
        CompletedAt = session.CompletedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        InterviewType = $"{session.Category} / {session.Difficulty}";
        SubmissionStatus = completionLabel;
        AdminReviewMessage = "You can now logout. The admin can review your detailed evaluation and feedback from the admin panel.";
        BuildTimeline(session);
    }

    [RelayCommand]
    private async Task DoneAsync()
    {
        await _navigator.LogoutAsync();
    }

    private void BuildTimeline(InterviewSession session)
    {
        var index = 1;
        foreach (var result in session.QuestionResults)
        {
            var isFollowUp = IsFollowUpResult(result);
            var skipped = result.Feedback.Contains("skipped", StringComparison.OrdinalIgnoreCase);
            TimelineItems.Add(new InterviewTimelineItem
            {
                QuestionId = result.QuestionId,
                Number = index++,
                Topic = isFollowUp ? "Follow-up" : ResolveQuestionTopic(result),
                Title = BuildTimelineTitle(result.Prompt),
                IsFollowUp = isFollowUp,
                Status = skipped ? "Skipped" : "Answered",
                Subtitle = isFollowUp ? "Adaptive follow-up saved" : "Saved for admin review",
                AccentBrush = skipped ? "#94A3B8" : isFollowUp ? "#F59E0B" : "#10B981",
                BadgeBackground = skipped ? "#F1F5F9" : isFollowUp ? "#FEF3C7" : "#D1FAE5",
                BadgeForeground = skipped ? "#475569" : isFollowUp ? "#92400E" : "#047857",
            });
        }
    }

    private static bool IsFollowUpResult(InterviewQuestionResult result)
    {
        return ExtractHintValue(result.IdealAnswerHint, "Section").Equals("followup", StringComparison.OrdinalIgnoreCase) ||
               result.Prompt.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveQuestionTopic(InterviewQuestionResult result)
    {
        var section = ExtractHintValue(result.IdealAnswerHint, "Section");
        if (!string.IsNullOrWhiteSpace(section))
        {
            return section.Trim().ToLowerInvariant() switch
            {
                "api" => "API",
                "hr" => "HR",
                "oop" => "OOP",
                "career" => "Career",
                "project" => "Project",
                "coding" => "Coding",
                "database" => "Database",
                "security" => "Security",
                "testing" => "Testing",
                "frontend" => "Frontend",
                "debugging" => "Debugging",
                "reliability" => "Reliability",
                "behavioral" => "Behavioral",
                "management" => "Management",
                "technical" => "Technical",
                _ => "Interview",
            };
        }

        return string.IsNullOrWhiteSpace(result.Topic) ? "Interview" : result.Topic;
    }

    private static string ExtractHintValue(string? hint, string key)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return string.Empty;
        }

        foreach (var part in hint.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = part[..separatorIndex].Trim();
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return part[(separatorIndex + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    private static string BuildTimelineTitle(string prompt)
    {
        var clean = string.IsNullOrWhiteSpace(prompt) ? "Interview question" : prompt.Trim();
        if (clean.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean["Follow-up:".Length..].Trim();
        }

        const int maxLength = 78;
        if (clean.Length <= maxLength)
        {
            return clean;
        }

        var cut = clean.LastIndexOf(' ', maxLength);
        return $"{clean[..(cut > 0 ? cut : maxLength)].TrimEnd('.', ',', ';')}...";
    }
}
