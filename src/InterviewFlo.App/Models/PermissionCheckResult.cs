namespace InterviewFlo.App.Models;

public sealed class PermissionCheckResult
{
    public bool MicrophoneGranted { get; init; }

    public bool CameraGranted { get; init; }

    public bool TranscriptionGranted { get; init; }

    public string Message { get; init; } = string.Empty;
}
