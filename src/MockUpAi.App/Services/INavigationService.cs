using MockUpAi.App.ViewModels;

namespace MockUpAi.App.Services;

public interface INavigationService
{
    event Action<ViewModelBase>? CurrentViewModelChanged;

    ViewModelBase? CurrentViewModel { get; }

    void Navigate(ViewModelBase viewModel);
}
