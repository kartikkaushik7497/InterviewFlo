using MockUpAi.App.Models;

namespace MockUpAi.App.Services.Media;

public interface IMicrophoneRecorderService
{
    bool IsRecording { get; }

    string LastError { get; }

    double CurrentInputLevel { get; }

    int SelectedDeviceIndex { get; }

    Task<IReadOnlyList<MediaDeviceOption>> GetAvailableMicrophonesAsync(CancellationToken cancellationToken = default);

    void SelectMicrophone(int deviceIndex);

    Task<bool> CanAccessMicrophoneAsync();

    Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default);

    Task<byte[]> GetLiveWavSnapshotAsync(CancellationToken cancellationToken = default);

    Task<byte[]> StopRecordingAsync(CancellationToken cancellationToken = default);
}
