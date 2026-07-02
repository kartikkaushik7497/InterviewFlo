using InterviewFlo.App.ViewModels;

namespace InterviewFlo.App.Services;

public interface INavigationService
{
    event Action<ViewModelBase>? CurrentViewModelChanged;

    ViewModelBase? CurrentViewModel { get; }

    void Navigate(ViewModelBase viewModel);
}
