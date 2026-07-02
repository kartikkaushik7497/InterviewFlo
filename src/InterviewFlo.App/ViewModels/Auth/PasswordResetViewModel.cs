using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InterviewFlo.App.Services;
using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.App.ViewModels.Auth;

public partial class PasswordResetViewModel : ViewModelBase
{
    private readonly SessionContext _session;
    private readonly IAuthService _authService;
    private readonly IAppNavigator _navigator;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetPasswordCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Password reset is required before continuing.";

    public PasswordResetViewModel(SessionContext session, IAuthService authService, IAppNavigator navigator)
    {
        _session = session;
        _authService = authService;
        _navigator = navigator;
    }

    private bool CanResetPassword()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(NewPassword) &&
               !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    [RelayCommand(CanExecute = nameof(CanResetPassword))]
    private async Task ResetPasswordAsync()
    {
        if (NewPassword != ConfirmPassword)
        {
            StatusMessage = "Password and confirm password must match.";
            return;
        }

        var userId = _session.PendingPasswordResetUserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            StatusMessage = "No reset session found.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.CompletePasswordResetAsync(userId, NewPassword);
            StatusMessage = result.Message;
            if (!result.Succeeded)
            {
                return;
            }

            _session.RequiresPasswordReset = false;
            _session.PendingPasswordResetUserId = string.Empty;

            if (_session.CurrentUser?.ActorType == UserActorType.Admin)
            {
                await _navigator.NavigateToAdminDashboardAsync();
            }
            else
            {
                await _navigator.NavigateToCandidatePermissionsAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _navigator.LogoutAsync();
    }
}
