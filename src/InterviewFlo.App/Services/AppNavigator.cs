using Microsoft.Extensions.DependencyInjection;
using InterviewFlo.App.ViewModels;
using InterviewFlo.App.ViewModels.Admin;
using InterviewFlo.App.ViewModels.Auth;
using InterviewFlo.App.ViewModels.Candidate;

namespace InterviewFlo.App.Services;

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
        _navigationService.Navigate(viewModel);
        await viewModel.InitializeAsync();
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
