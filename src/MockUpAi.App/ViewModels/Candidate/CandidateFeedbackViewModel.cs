using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.Core.Application.Abstractions;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateFeedbackViewModel : ViewModelBase
{
    private readonly SessionContext _session;
    private readonly IInterviewRepository _interviewRepository;
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitFeedbackCommand))]
    private int _selectedRating;

    [ObservableProperty]
    private string _suggestions = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Please rate your interview experience and add suggestions.";

    [ObservableProperty]
    private bool _isSubmitted;

    [ObservableProperty]
    private string _star1 = "\u2606";

    [ObservableProperty]
    private string _star2 = "\u2606";

    [ObservableProperty]
    private string _star3 = "\u2606";

    [ObservableProperty]
    private string _star4 = "\u2606";

    [ObservableProperty]
    private string _star5 = "\u2606";

    public CandidateFeedbackViewModel(
        SessionContext session,
        IInterviewRepository interviewRepository,
        IAppNavigator navigator)
    {
        _session = session;
        _interviewRepository = interviewRepository;
        _navigator = navigator;
    }

    public void Initialize()
    {
        var interview = _session.LastInterview;
        if (interview is null)
        {
            StatusMessage = "No interview session found.";
            IsSubmitted = false;
            SelectedRating = 0;
            Suggestions = string.Empty;
            UpdateStars();
            return;
        }

        SelectedRating = Math.Clamp(interview.FeedbackRating, 0, 5);
        Suggestions = interview.FeedbackSuggestions ?? string.Empty;
        IsSubmitted = interview.FeedbackSubmittedAtUtc.HasValue;
        StatusMessage = IsSubmitted
            ? "Feedback already submitted. You can continue."
            : "Please rate your interview experience and add suggestions.";
        UpdateStars();
    }

    [RelayCommand]
    private void SetRating(int rating)
    {
        if (rating < 1 || rating > 5)
        {
            return;
        }

        SelectedRating = rating;
        IsSubmitted = false;
        StatusMessage = $"You selected {rating} star{(rating > 1 ? "s" : string.Empty)}.";
    }

    private bool CanSubmitFeedback()
    {
        return SelectedRating >= 1 && SelectedRating <= 5 && !IsSubmitted;
    }

    [RelayCommand(CanExecute = nameof(CanSubmitFeedback))]
    private async Task SubmitFeedbackAsync()
    {
        var interview = _session.LastInterview;
        if (interview is null)
        {
            StatusMessage = "No interview session found.";
            return;
        }

        if (SelectedRating < 1 || SelectedRating > 5)
        {
            StatusMessage = "Please select a rating between 1 and 5 stars.";
            return;
        }

        interview.FeedbackRating = SelectedRating;
        interview.FeedbackSuggestions = Suggestions.Trim();
        interview.FeedbackSubmittedAtUtc = DateTime.UtcNow;

        await _interviewRepository.SaveAsync(interview);

        IsSubmitted = true;
        StatusMessage = "Thank you. Feedback submitted successfully.";
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        if (!IsSubmitted)
        {
            StatusMessage = "Submit feedback before continuing.";
            return;
        }

        await _navigator.NavigateToCandidateResultAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _navigator.LogoutAsync();
    }

    partial void OnSelectedRatingChanged(int value)
    {
        UpdateStars();
    }

    private void UpdateStars()
    {
        Star1 = SelectedRating >= 1 ? "\u2605" : "\u2606";
        Star2 = SelectedRating >= 2 ? "\u2605" : "\u2606";
        Star3 = SelectedRating >= 3 ? "\u2605" : "\u2606";
        Star4 = SelectedRating >= 4 ? "\u2605" : "\u2606";
        Star5 = SelectedRating >= 5 ? "\u2605" : "\u2606";
    }
}
