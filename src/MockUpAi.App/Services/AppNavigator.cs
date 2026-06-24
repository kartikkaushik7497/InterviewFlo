using Microsoft.Extensions.DependencyInjection;
using MockUpAi.App.ViewModels;
using MockUpAi.App.ViewModels.Admin;
using MockUpAi.App.ViewModels.Auth;
using MockUpAi.App.ViewModels.Candidate;

namespace MockUpAi.App.Services;

public sealed class AppNavigator : IAppNavigator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly SessionContext _sessionContext;

    public AppNavigator(
        IServiceProvider serviceProvider,
        INavigationService navigationService,
        SessionContext sessionContext)
    {
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        _sessionContext = sessionContext;
    }

    public Task NavigateToLoginAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
        _navigationService.Navigate(viewModel);
        return Task.CompletedTask;
    }

    public Task NavigateToPasswordResetAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<PasswordResetViewModel>();
        _navigationService.Navigate(viewModel);
        return Task.CompletedTask;
    }

    public async Task NavigateToAdminDashboardAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<AdminDashboardViewModel>();
        await viewModel.LoadAsync();
        _navigationService.Navigate(viewModel);
    }

    public async Task NavigateToCandidatePermissionsAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<CandidatePermissionViewModel>();
        viewModel.PrepareForPermissionRequest();
        _navigationService.Navigate(viewModel);
        await Task.CompletedTask;
    }

    public Task NavigateToCandidateRulesAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<CandidateRulesViewModel>();
        viewModel.Initialize();
        _navigationService.Navigate(viewModel);
        return Task.CompletedTask;
    }

    public Task NavigateToCandidateLobbyAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<CandidateLobbyViewModel>();
        viewModel.StartTimer();
        _navigationService.Navigate(viewModel);
        return Task.CompletedTask;
    }

    public async Task NavigateToCandidateInterviewAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<CandidateInterviewViewModel>();
        await viewModel.InitializeAsync();
        _navigationService.Navigate(viewModel);
    }

    public Task NavigateToCandidateResultAsync()
    {
        var viewModel = _serviceProvider.GetRequiredService<CandidateResultViewModel>();
        viewModel.LoadFromSession(_sessionContext);
        _navigationService.Navigate(viewModel);
        return Task.CompletedTask;
    }

    public Task NavigateToCandidateFeedbackAsync()
    {
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<CandidateFeedbackViewModel>();
            viewModel.Initialize();
            _navigationService.Navigate(viewModel);
        }
        catch
        {
            // Ensure candidate is never stranded on a blank screen when feedback bootstrap fails.
            var fallback = _serviceProvider.GetRequiredService<CandidateResultViewModel>();
            fallback.LoadFromSession(_sessionContext);
            _navigationService.Navigate(fallback);
        }

        return Task.CompletedTask;
    }

    public async Task LogoutAsync()
    {
        _sessionContext.Clear();
        await NavigateToLoginAsync();
    }
}
