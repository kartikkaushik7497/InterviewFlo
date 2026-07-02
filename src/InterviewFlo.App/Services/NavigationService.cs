using InterviewFlo.App.ViewModels;

namespace InterviewFlo.App.Services;

public sealed class NavigationService : INavigationService
{
    public event Action<ViewModelBase>? CurrentViewModelChanged;

    public ViewModelBase? CurrentViewModel { get; private set; }

    public void Navigate(ViewModelBase viewModel)
    {
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(viewModel);
    }
}
