using Avalonia.Media.Imaging;

namespace InterviewFlo.App.Services.Media;

public interface ICameraPreviewService
{
    event Action<Bitmap>? FrameReady;

    bool IsRunning { get; }

    string LastError { get; }

    Task<bool> CanAccessCameraAsync();

    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
