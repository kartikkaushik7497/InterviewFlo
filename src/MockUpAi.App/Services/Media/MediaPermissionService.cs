using Microsoft.Extensions.Options;
using MockUpAi.App.Models;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Infrastructure.Configuration;

namespace MockUpAi.App.Services.Media;

public sealed class MediaPermissionService : IMediaPermissionService
{
    private readonly IMicrophoneRecorderService _microphone;
    private readonly ICameraPreviewService _camera;
    private readonly ISecretVaultService _secretVault;
    private readonly bool _openAiEnabled;

    public MediaPermissionService(
        IMicrophoneRecorderService microphone,
        ICameraPreviewService camera,
        ISecretVaultService secretVault,
        IOptions<OpenAiSettings> openAiSettings)
    {
        _microphone = microphone;
        _camera = camera;
        _secretVault = secretVault;
        _openAiEnabled = openAiSettings.Value.Enabled;
    }

    public async Task<PermissionCheckResult> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var mic = await _microphone.CanAccessMicrophoneAsync();
        var camera = await _camera.CanAccessCameraAsync();
        var hasTranscription = !_openAiEnabled || await _secretVault.HasOpenAiApiKeyAsync(cancellationToken);

        var message = BuildMessage(mic, camera, hasTranscription);

        return new PermissionCheckResult
        {
            MicrophoneGranted = mic,
            CameraGranted = camera,
            TranscriptionGranted = hasTranscription,
            Message = message,
        };
    }

    private static string BuildMessage(bool mic, bool camera, bool transcription)
    {
        if (camera && mic && transcription)
        {
            return "All required permissions and AI key checks passed.";
        }

        if (camera && mic && !transcription)
        {
            return "Camera and microphone are available. Speech transcription key is missing; manual typing fallback can be used.";
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

        if (!transcription)
        {
            issues.Add("OpenAI API key missing for speech transcription.");
        }

        return string.Join(" ", issues);
    }
}
