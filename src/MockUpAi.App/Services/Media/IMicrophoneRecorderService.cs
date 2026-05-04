namespace MockUpAi.App.Services.Media;

public interface IMicrophoneRecorderService
{
    bool IsRecording { get; }

    string LastError { get; }

    Task<bool> CanAccessMicrophoneAsync();

    Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default);

    Task<byte[]> StopRecordingAsync(CancellationToken cancellationToken = default);
}
