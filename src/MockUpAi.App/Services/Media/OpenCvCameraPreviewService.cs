using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace MockUpAi.App.Services.Media;

public sealed class OpenCvCameraPreviewService : ICameraPreviewService
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public event Action<Bitmap>? FrameReady;

    public bool IsRunning { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public Task<bool> CanAccessCameraAsync()
    {
        try
        {
            using var probe = new VideoCapture(0);
            if (!probe.IsOpened())
            {
                LastError = "Unable to access camera device.";
                return Task.FromResult(false);
            }

            LastError = string.Empty;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LastError = $"Camera access error: {ex.Message}";
            return Task.FromResult(false);
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return true;
        }

        try
        {
            _capture = new VideoCapture(0);
            if (!_capture.IsOpened())
            {
                LastError = "Unable to open camera stream.";
                _capture.Dispose();
                _capture = null;
                return false;
            }

            _capture.Fps = 24;
            _capture.FrameWidth = 640;
            _capture.FrameHeight = 360;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loopTask = Task.Run(() => CaptureLoopAsync(_cts.Token), CancellationToken.None);
            IsRunning = true;
            LastError = string.Empty;

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Camera preview failed: {ex.Message}";
            return false;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            if (_loopTask is not null)
            {
                await _loopTask;
            }
        }
        catch
        {
            // ignore cleanup exceptions
        }
        finally
        {
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;

            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
            IsRunning = false;
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        using var frame = new Mat();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_capture is null)
            {
                break;
            }

            try
            {
                var ok = _capture.Read(frame);
                if (!ok || frame.Empty())
                {
                    await Task.Delay(60, cancellationToken);
                    continue;
                }

                using var converted = frame.CvtColor(ColorConversionCodes.BGR2BGRA);
                var bytes = converted.ToBytes(".bmp");

                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                FrameReady?.Invoke(bitmap);

                await Task.Delay(40, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = $"Camera frame error: {ex.Message}";
                break;
            }
        }
    }
}
