using MockUpAi.App.ViewModels;

namespace MockUpAi.App.Services;

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
