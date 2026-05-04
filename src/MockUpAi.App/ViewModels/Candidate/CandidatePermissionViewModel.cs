using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MockUpAi.App.Services;
using MockUpAi.App.Services.Media;

namespace MockUpAi.App.ViewModels.Candidate;

public partial class CandidatePermissionViewModel : ViewModelBase
{
    private readonly IAppNavigator _navigator;
    private readonly IMediaPermissionService _permissionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _microphoneAccessGranted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _cameraAccessGranted;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    private bool _speechTranscriptionAccessGranted;

    [ObservableProperty]
    private string _statusMessage = "Checking permissions...";

    [ObservableProperty]
    private bool _isChecking;

    public CandidatePermissionViewModel(IAppNavigator navigator, IMediaPermissionService permissionService)
    {
        _navigator = navigator;
        _permissionService = permissionService;
    }

    public async Task CheckPermissionsAsync()
    {
        IsChecking = true;
        try
        {
            var result = await _permissionService.CheckAllAsync();
            MicrophoneAccessGranted = result.MicrophoneGranted;
            CameraAccessGranted = result.CameraGranted;
            SpeechTranscriptionAccessGranted = result.TranscriptionGranted;
            StatusMessage = result.Message;
        }
        finally
        {
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
}
