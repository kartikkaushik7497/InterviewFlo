using Avalonia.Media.Imaging;
using MockUpAi.App.Models;

namespace MockUpAi.App.Services.Media;

public interface ICameraPreviewService
{
    event Action<Bitmap>? FrameReady;

    bool IsRunning { get; }

    string LastError { get; }

    int SelectedDeviceIndex { get; }

    Task<IReadOnlyList<MediaDeviceOption>> GetAvailableCamerasAsync(CancellationToken cancellationToken = default);

    void SelectCamera(int deviceIndex);

    Task<bool> CanAccessCameraAsync();

    Task<bool> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
