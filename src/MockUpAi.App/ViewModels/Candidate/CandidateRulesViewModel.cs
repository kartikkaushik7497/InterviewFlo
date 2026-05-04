using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateRulesViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    private string _rulesText =
        "1. Stay visible in camera throughout the interview.\n" +
        "2. Speak clearly while answering each question.\n" +
        "3. Avoid external assistance during the session.\n" +
        "4. Keep answers concise, practical, and role-focused.\n" +
        "5. You can proceed after reviewing all guidance.";

    public CandidateRulesViewModel(IAppNavigator navigator)
    {
        _navigator = navigator;
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        await _navigator.NavigateToCandidateLobbyAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _navigator.LogoutAsync();
    }
}
