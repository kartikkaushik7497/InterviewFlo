using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Models;
using MockUpAi.App.Services;
using MockUpAi.App.Services.Media;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidatePermissionViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;
    private readonly IMediaPermissionService _permissionService;
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ICameraPreviewService _camera;
    private CancellationTokenSource? _spinnerCts;
    private bool _isRefreshingDevices;

    public ObservableCollection<MediaDeviceOption> MicrophoneDevices { get; } = [];

    public ObservableCollection<MediaDeviceOption> CameraDevices { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private MediaDeviceOption? _selectedMicrophone;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private MediaDeviceOption? _selectedCamera;

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
            await RefreshDevicesAsync();
            ApplySelectedDevices();

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
        return MicrophoneAccessGranted && CameraAccessGranted && SelectedMicrophone is not null && SelectedCamera is not null;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        ApplySelectedDevices();
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
            await RefreshDevicesAsync();
            ApplySelectedDevices();
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
        _ = RefreshDevicesAsync();
    }

    partial void OnSelectedMicrophoneChanged(MediaDeviceOption? value)
    {
        if (_isRefreshingDevices)
        {
            return;
        }

        MicrophoneAccessGranted = false;
        if (value is not null)
        {
            _microphone.SelectMicrophone(value.DeviceIndex);
            StatusMessage = "Microphone changed. Click Recheck or Request Access before continuing.";
        }
    }

    partial void OnSelectedCameraChanged(MediaDeviceOption? value)
    {
        if (_isRefreshingDevices)
        {
            return;
        }

        CameraAccessGranted = false;
        if (value is not null)
        {
            _camera.SelectCamera(value.DeviceIndex);
            StatusMessage = "Camera changed. Click Recheck or Request Access before continuing.";
        }
    }

    private async Task RefreshDevicesAsync()
    {
        _isRefreshingDevices = true;
        try
        {
            var microphones = await _microphone.GetAvailableMicrophonesAsync();
            var cameras = await _camera.GetAvailableCamerasAsync();

            ReplaceDevices(MicrophoneDevices, microphones);
            ReplaceDevices(CameraDevices, cameras);

            SelectedMicrophone = SelectExistingOrFirst(MicrophoneDevices, SelectedMicrophone?.DeviceIndex ?? _microphone.SelectedDeviceIndex);
            SelectedCamera = SelectExistingOrFirst(CameraDevices, SelectedCamera?.DeviceIndex ?? _camera.SelectedDeviceIndex);
        }
        finally
        {
            _isRefreshingDevices = false;
        }
    }

    private void ApplySelectedDevices()
    {
        if (SelectedMicrophone is not null)
        {
            _microphone.SelectMicrophone(SelectedMicrophone.DeviceIndex);
        }

        if (SelectedCamera is not null)
        {
            _camera.SelectCamera(SelectedCamera.DeviceIndex);
        }
    }

    private static void ReplaceDevices(ObservableCollection<MediaDeviceOption> target, IReadOnlyList<MediaDeviceOption> source)
    {
        target.Clear();
        foreach (var device in source)
        {
            target.Add(device);
        }
    }

    private static MediaDeviceOption? SelectExistingOrFirst(
        IReadOnlyList<MediaDeviceOption> devices,
        int preferredDeviceIndex)
    {
        return devices.FirstOrDefault(device => device.DeviceIndex == preferredDeviceIndex)
            ?? devices.FirstOrDefault();
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
