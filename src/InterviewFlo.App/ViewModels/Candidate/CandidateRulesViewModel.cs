using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InterviewFlo.App.Services;

namespace InterviewFlo.App.ViewModels.Candidate;

public partial class CandidateRulesViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;
    private readonly SessionContext _session;

    [ObservableProperty]
    private string _rulesText =
        "1. Stay visible in camera throughout the interview.\n" +
        "2. Speak clearly while answering each question.\n" +
        "3. Avoid external assistance during the session.\n" +
        "4. Keep answers concise, practical, and role-focused.\n" +
        "5. You can proceed after reviewing all guidance.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _hasAcknowledgedRules;

    [ObservableProperty]
    private string _statusMessage = "Please acknowledge the rules before continuing.";

    public CandidateRulesViewModel(IAppNavigator navigator, SessionContext session)
    {
        _navigator = navigator;
        _session = session;
    }

    public void Initialize()
    {
        HasAcknowledgedRules = _session.RulesAcknowledged;
        StatusMessage = HasAcknowledgedRules
            ? "Acknowledgment captured. You can proceed."
            : "Please acknowledge the rules before continuing.";
    }

    private bool CanContinue()
    {
        return HasAcknowledgedRules;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        if (!HasAcknowledgedRules)
        {
            StatusMessage = "You must acknowledge the rules and permissions to continue.";
            return;
        }

        _session.RulesAcknowledged = true;
        _session.RulesAcknowledgedAtUtc = DateTime.UtcNow;
        await _navigator.NavigateToCandidateLobbyAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _navigator.LogoutAsync();
    }

    partial void OnHasAcknowledgedRulesChanged(bool value)
    {
        if (value)
        {
            _session.RulesAcknowledged = true;
            _session.RulesAcknowledgedAtUtc ??= DateTime.UtcNow;
            StatusMessage = "Acknowledgment captured. You can proceed.";
            return;
        }

        _session.RulesAcknowledged = false;
        _session.RulesAcknowledgedAtUtc = null;
        StatusMessage = "Please acknowledge the rules before continuing.";
    }
}
