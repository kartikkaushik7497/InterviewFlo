using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MockUpAi.App.ViewModels.Admin;

namespace MockUpAi.App.Views.Admin;

public partial class AdminDashboardView : UserControl
{
    private INotifyPropertyChanged? _currentViewModel;

    public AdminDashboardView()
    {
        InitializeComponent();
        CandidateGrid.PointerReleased += OnCandidateGridPointerReleased;
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            CandidateGrid.PointerReleased -= OnCandidateGridPointerReleased;
            DetachViewModel();
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachViewModel();

        if (DataContext is INotifyPropertyChanged viewModel)
        {
            _currentViewModel = viewModel;
            _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AdminDashboardViewModel.SelectedCandidate) ||
            DataContext is not AdminDashboardViewModel { SelectedCandidate: not null })
        {
            return;
        }

        Dispatcher.UIThread.Post(ScrollToSelectedCandidateEditor, DispatcherPriority.Background);
    }

    private void OnCandidateGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is AdminDashboardViewModel { SelectedCandidate: not null })
        {
            Dispatcher.UIThread.Post(ScrollToSelectedCandidateEditor, DispatcherPriority.Background);
        }
    }

    private void ScrollToSelectedCandidateEditor()
    {
        if (!EditCandidatePanel.IsVisible)
        {
            return;
        }

        EditCandidatePanel.BringIntoView();
    }

    private void DetachViewModel()
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _currentViewModel = null;
        }
    }
}
