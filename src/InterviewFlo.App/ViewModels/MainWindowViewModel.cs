using CommunityToolkit.Mvvm.ComponentModel;
using InterviewFlo.App.Services;

namespace InterviewFlo.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string AppTitle => "InterviewFlo";

    public string AppSubtitle => "AI-Powered Mock Interview Platform";

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    public MainWindowViewModel(INavigationService navigationService)
    {
        CurrentPage = navigationService.CurrentViewModel;
        navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
    }

    private void OnCurrentViewModelChanged(ViewModelBase viewModel)
    {
        CurrentPage = viewModel;
    }
}
