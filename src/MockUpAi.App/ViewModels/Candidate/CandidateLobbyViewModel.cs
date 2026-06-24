using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidateLobbyViewModel : ViewModelBase
{
    private const int DefaultLobbySeconds = 15;
    private readonly IAppNavigator _navigator;
    private readonly DispatcherTimer _timer;
    private bool _navigating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountdownDisplay))]
    private int _remainingSeconds = DefaultLobbySeconds;

    [ObservableProperty]
    private string _statusMessage = "Interview starts automatically in a few seconds.";

    public string CountdownDisplay => TimeSpan.FromSeconds(RemainingSeconds).ToString("mm\\:ss");

    public CandidateLobbyViewModel(IAppNavigator navigator)
    {
        _navigator = navigator;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += OnTimerTick;
    }

    public void StartTimer()
    {
        _navigating = false;
        RemainingSeconds = DefaultLobbySeconds;
        StatusMessage = "Interview starts automatically in a few seconds.";
        _timer.Start();
    }

    [RelayCommand]
    private async Task StartNowAsync()
    {
        await BeginInterviewAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _timer.Stop();
        await _navigator.LogoutAsync();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (RemainingSeconds <= 0)
        {
            _timer.Stop();
            _ = BeginInterviewAsync();
            return;
        }

        RemainingSeconds--;
    }

    private async Task BeginInterviewAsync()
    {
        if (_navigating)
        {
            return;
        }

        _navigating = true;
        _timer.Stop();
        StatusMessage = "Starting interview session...";
        await _navigator.NavigateToCandidateInterviewAsync();
    }
}
