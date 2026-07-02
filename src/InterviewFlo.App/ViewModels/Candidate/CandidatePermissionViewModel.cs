using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InterviewFlo.App.Services;
using InterviewFlo.App.Services.Media;
using System.Diagnostics;

namespace InterviewFlo.App.ViewModels.Candidate;

public partial class CandidatePermissionViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;
    private readonly IMediaPermissionService _permissionService;
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ICameraPreviewService _camera;
    private CancellationTokenSource? _spinnerCts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _microphoneAccessGranted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _cameraAccessGranted;

    [ObservableProperty]
    private string _statusMessage = "Click Request Access, then allow camera and microphone.";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private double _loadingAngle;

    public CandidatePermissionViewModel(
        IAppNavigator navigator,
        IMediaPermissionService permissionService,
        IMicrophoneRecorderService microphone,
        ICameraPreviewService camera)
    {
        _navigator = navigator;
        _permissionService = permissionService;
        _microphone = microphone;
        _camera = camera;
    }

    public async Task CheckPermissionsAsync()
    {
        IsChecking = true;
        StartSpinner();
        try
        {
            var timer = Stopwatch.StartNew();
            var result = await _permissionService.CheckAllAsync();
            var remainingDelay = TimeSpan.FromSeconds(2.5) - timer.Elapsed;
            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay);
            }

            MicrophoneAccessGranted = result.MicrophoneGranted;
            CameraAccessGranted = result.CameraGranted;
            StatusMessage = BuildPermissionMessage(result.MicrophoneGranted, result.CameraGranted);
        }
        finally
        {
            StopSpinner();
            IsChecking = false;
        }
    }

    private bool CanContinue()
    {
        return MicrophoneAccessGranted && CameraAccessGranted;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        StatusMessage = string.Empty;
        await _navigator.NavigateToCandidateRulesAsync();
    }

    [RelayCommand]
    private async Task RecheckAsync()
    {
        await CheckPermissionsAsync();
    }

    [RelayCommand]
    private async Task RequestAccessAsync()
    {
        IsChecking = true;
        StartSpinner();
        try
        {
            StatusMessage = "Requesting microphone and camera access...";

            var micStarted = await _microphone.StartRecordingAsync();
            if (micStarted)
            {
                await Task.Delay(250);
                await _microphone.StopRecordingAsync();
            }

            var camStarted = await _camera.StartAsync();
            if (camStarted)
            {
                await Task.Delay(300);
                await _camera.StopAsync();
            }

            var result = await _permissionService.CheckAllAsync();
            MicrophoneAccessGranted = result.MicrophoneGranted;
            CameraAccessGranted = result.CameraGranted;
            StatusMessage = BuildPermissionMessage(result.MicrophoneGranted, result.CameraGranted);
        }
        finally
        {
            StopSpinner();
            IsChecking = false;
        }
    }

    public void PrepareForPermissionRequest()
    {
        MicrophoneAccessGranted = false;
        CameraAccessGranted = false;
        StatusMessage = "Click Request Access, then allow camera and microphone.";
    }

    private static string BuildPermissionMessage(bool micGranted, bool cameraGranted)
    {
        if (micGranted && cameraGranted)
        {
            return "Camera and microphone checks passed.";
        }

        var issues = new List<string>();
        if (!micGranted)
        {
            issues.Add("Microphone is unavailable or blocked by OS privacy settings.");
        }

        if (!cameraGranted)
        {
            issues.Add("Camera is unavailable or blocked by OS privacy settings.");
        }

        return string.Join(" ", issues);
    }

    private void StartSpinner()
    {
        StopSpinner();
        _spinnerCts = new CancellationTokenSource();
        var token = _spinnerCts.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                Dispatcher.UIThread.Post(() => { LoadingAngle = (LoadingAngle + 24) % 360; });

                try
                {
                    await Task.Delay(80, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }, token);
    }

    private void StopSpinner()
    {
        if (_spinnerCts is null)
        {
            return;
        }

        _spinnerCts.Cancel();
        _spinnerCts.Dispose();
        _spinnerCts = null;
    }
}
