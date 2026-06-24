using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.App.ViewModels.Auth;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly SessionContext _sessionContext;
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _userId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _revealPassword;

    [ObservableProperty]
    private bool _isPasswordHidden = true;

    public LoginViewModel(
        IAuthService authService,
        SessionContext sessionContext,
        IAppNavigator navigator)
    {
        _authService = authService;
        _sessionContext = sessionContext;
        _navigator = navigator;
    }

    private bool CanLogin()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(UserId) &&
               !string.IsNullOrWhiteSpace(Password);
    }

    partial void OnStatusMessageChanged(string value)
    {
        HasStatusMessage = !string.IsNullOrWhiteSpace(value);
    }

    partial void OnRevealPasswordChanged(bool value)
    {
        IsPasswordHidden = !value;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Authenticating...";
            var result = await _authService.LoginAsync(UserId, Password);
            if (!result.IsSuccess || result.User is null)
            {
                StatusMessage = result.Message;
                return;
            }

            _sessionContext.CurrentUser = result.User;
            _sessionContext.LastInterview = null;
            _sessionContext.RequiresPasswordReset = false;
            _sessionContext.PendingPasswordResetUserId = string.Empty;
            StatusMessage = string.Empty;

            if (result.User.ActorType == UserActorType.Admin)
            {
                await _navigator.NavigateToAdminDashboardAsync();
                return;
            }

            await _navigator.NavigateToCandidatePermissionsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleRevealPassword()
    {
        RevealPassword = !RevealPassword;
    }
}
