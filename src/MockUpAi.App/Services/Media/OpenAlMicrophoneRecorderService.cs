using System.IO;
using NAudio.Wave;
using OpenTK.Audio.OpenAL;

namespace MockUpAi.App.Services.Media;

public sealed class OpenAlMicrophoneRecorderService : IMicrophoneRecorderService
{
    private const int SampleRate = 16000;

    private readonly object _sync = new();
    private readonly List<short> _capturedSamples = [];

    private CancellationTokenSource? _captureCts;
    private Task? _captureLoop;
    private ALCaptureDevice _captureDevice;

    private readonly object _waveSync = new();
    private WaveInEvent? _waveIn;
    private MemoryStream? _wavePcmBuffer;
    private bool _usingWaveIn;

    public bool IsRecording { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public Task<bool> CanAccessMicrophoneAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            return Task.FromResult(CanAccessWithWaveIn());
        }

        return Task.FromResult(CanAccessWithOpenAl());
    }

    public async Task<bool> StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (IsRecording)
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            return await StartRecordingWithWaveInAsync(cancellationToken);
        }

        return await StartRecordingWithOpenAlAsync(cancellationToken);
    }

    public async Task<byte[]> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording)
        {
            return [];
        }

        if (_usingWaveIn)
        {
            return await StopRecordingWithWaveInAsync(cancellationToken);
        }

        return await StopRecordingWithOpenAlAsync(cancellationToken);
    }

    private bool CanAccessWithWaveIn()
    {
        try
        {
            var count = WaveInEvent.DeviceCount;
            if (count <= 0)
            {
                LastError = "No microphone device detected on this system.";
                return false;
            }

            _ = WaveInEvent.GetCapabilities(0);
            LastError = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Windows microphone access error: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> StartRecordingWithWaveInAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!CanAccessWithWaveIn())
            {
                return false;
            }

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                BufferMilliseconds = 100,
                WaveFormat = new WaveFormat(SampleRate, 16, 1),
            };

            _wavePcmBuffer = new MemoryStream();
            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.StartRecording();

            _usingWaveIn = true;
            IsRecording = true;
            LastError = string.Empty;

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Unable to start microphone recording: {ex.Message}";
            CleanupWaveIn();
            IsRecording = false;
            return false;
        }
    }

    private async Task<byte[]> StopRecordingWithWaveInAsync(CancellationToken cancellationToken)
    {
        try
        {
            _waveIn?.StopRecording();
            await Task.Delay(100, cancellationToken);

            byte[] pcm;
            lock (_waveSync)
            {
                pcm = _wavePcmBuffer?.ToArray() ?? [];
            }

            return BuildWavFromPcm(pcm, SampleRate, 1, 16);
        }
        catch (Exception ex)
        {
            LastError = $"Unable to stop recording cleanly: {ex.Message}";
            return [];
        }
        finally
        {
            CleanupWaveIn();
            IsRecording = false;
            _usingWaveIn = false;
        }
    }

    private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_waveSync)
        {
            _wavePcmBuffer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void CleanupWaveIn()
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnWaveInDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }

        lock (_waveSync)
        {
            _wavePcmBuffer?.Dispose();
            _wavePcmBuffer = null;
        }
    }

    private bool CanAccessWithOpenAl()
    {
        try
        {
            var device = ALC.CaptureOpenDevice(null, SampleRate, ALFormat.Mono16, SampleRate);
            if (device.Equals(default(ALCaptureDevice)))
            {
                LastError = "Microphone capture device could not be opened.";
                return false;
            }

            ALC.CaptureCloseDevice(device);
            LastError = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Microphone access error: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> StartRecordingWithOpenAlAsync(CancellationToken cancellationToken)
    {
        try
        {
            _captureDevice = ALC.CaptureOpenDevice(null, SampleRate, ALFormat.Mono16, SampleRate * 5);
            if (_captureDevice.Equals(default(ALCaptureDevice)))
            {
                LastError = "Unable to open microphone capture device.";
                return false;
            }

            lock (_sync)
            {
                _capturedSamples.Clear();
            }

            _captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRecording = true;
            _usingWaveIn = false;
            LastError = string.Empty;

            ALC.CaptureStart(_captureDevice);
            _captureLoop = Task.Run(() => CaptureLoopAsync(_captureCts.Token), CancellationToken.None);

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Unable to start microphone recording: {ex.Message}";
            IsRecording = false;
            return false;
        }
    }

    private async Task<byte[]> StopRecordingWithOpenAlAsync(CancellationToken cancellationToken)
    {
        try
        {
            _captureCts?.Cancel();
            if (_captureLoop is not null)
            {
                await _captureLoop;
            }

            ALC.CaptureStop(_captureDevice);
            ALC.CaptureCloseDevice(_captureDevice);
        }
        catch (Exception ex)
        {
            LastError = $"Unable to stop recording cleanly: {ex.Message}";
        }
        finally
        {
            IsRecording = false;
            _captureCts?.Dispose();
            _captureCts = null;
            _captureLoop = null;
        }

        lock (_sync)
        {
            return BuildWav(_capturedSamples, SampleRate);
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sampleCounts = new int[1];
                ALC.GetInteger(_captureDevice, AlcGetInteger.CaptureSamples, 1, sampleCounts);
                var availableSamples = sampleCounts[0];

                if (availableSamples > 0)
                {
                    var buffer = new short[availableSamples];
                    ALC.CaptureSamples(_captureDevice, ref buffer[0], availableSamples);

                    lock (_sync)
                    {
                        _capturedSamples.AddRange(buffer);
                    }
                }

                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = $"Microphone capture interrupted: {ex.Message}";
                break;
            }
        }
    }

    private static byte[] BuildWav(IReadOnlyList<short> samples, int sampleRate)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);

        var bytesPerSample = 2;
        var channelCount = 1;
        var byteRate = sampleRate * channelCount * bytesPerSample;
        var blockAlign = (short)(channelCount * bytesPerSample);
        var dataSize = samples.Count * bytesPerSample;

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channelCount);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write((short)(bytesPerSample * 8));
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return memory.ToArray();
    }

    private static byte[] BuildWavFromPcm(byte[] pcmBytes, int sampleRate, short channels, short bitsPerSample)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);

        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = (short)(channels * (bitsPerSample / 8));

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + pcmBytes.Length);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);

        writer.Flush();
        return memory.ToArray();
    }
}
