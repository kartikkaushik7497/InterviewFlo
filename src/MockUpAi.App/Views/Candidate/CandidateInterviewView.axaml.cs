using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MockUpAi.App.ViewModels.Candidate;

namespace MockUpAi.App.Views.Candidate;

public partial class CandidateInterviewView : UserControl
{
    private CandidateInterviewViewModel? _currentViewModel;

    public CandidateInterviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => DetachViewModel();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachViewModel();

        if (DataContext is CandidateInterviewViewModel viewModel)
        {
            _currentViewModel = viewModel;
            _currentViewModel.ChatMessages.CollectionChanged += OnChatMessagesChanged;
        }
    }

    private void OnChatMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        PostScrollToBottom(ConversationScrollViewer);
    }

    private void OnTimelineLightDismissPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is CandidateInterviewViewModel viewModel &&
            viewModel.CloseTimelineCommand.CanExecute(null))
        {
            viewModel.CloseTimelineCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnTimelinePopupPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private static void PostScrollToBottom(ScrollViewer scrollViewer)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                scrollViewer.Offset = new Vector(
                    scrollViewer.Offset.X,
                    Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height));
            }, DispatcherPriority.Loaded);
        }, DispatcherPriority.Background);
    }

    private void DetachViewModel()
    {
        if (_currentViewModel is not null)
        {
            _currentViewModel.ChatMessages.CollectionChanged -= OnChatMessagesChanged;
            _currentViewModel = null;
        }
    }
}
