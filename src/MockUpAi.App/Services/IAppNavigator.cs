namespace MockUpAi.App.Services;

public interface IAppNavigator
{
    Task NavigateToLoginAsync();

    Task NavigateToPasswordResetAsync();

    Task NavigateToAdminDashboardAsync();

    Task NavigateToCandidatePermissionsAsync();

    Task NavigateToCandidateRulesAsync();

    Task NavigateToCandidateLobbyAsync();

    Task NavigateToCandidateInterviewAsync();

    Task NavigateToCandidateFeedbackAsync();

    Task NavigateToCandidateResultAsync();

    Task LogoutAsync();
}
