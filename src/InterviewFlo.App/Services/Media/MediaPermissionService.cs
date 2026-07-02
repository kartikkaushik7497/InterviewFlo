using InterviewFlo.App.Models;

namespace InterviewFlo.App.Services.Media;

public sealed class MediaPermissionService : IMediaPermissionService
{
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ICameraPreviewService _camera;

    public MediaPermissionService(
        IMicrophoneRecorderService microphone,
        ICameraPreviewService camera)
    {
        _microphone = microphone;
        _camera = camera;
    }

    public async Task<PermissionCheckResult> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var mic = await _microphone.CanAccessMicrophoneAsync();
        var camera = await _camera.CanAccessCameraAsync();
        var message = BuildMessage(mic, camera);

        return new PermissionCheckResult
        {
            MicrophoneGranted = mic,
            CameraGranted = camera,
            TranscriptionGranted = true,
            Message = message,
        };
    }

    private static string BuildMessage(bool mic, bool camera)
    {
        if (camera && mic)
        {
            return "Camera and microphone checks passed.";
        }

        var issues = new List<string>();
        if (!mic)
        {
            issues.Add("Microphone is required but unavailable or blocked by OS privacy settings.");
        }

        if (!camera)
        {
            issues.Add("Camera is required but unavailable or blocked by OS privacy settings.");
        }

        return string.Join(" ", issues);
    }
}
