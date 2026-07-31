using System.Text;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Abstractions;
using Whisper.net;
using Whisper.net.Ggml;

namespace MockUpAi.App.Services.Media;

[SupportedOSPlatform("windows")]
public sealed class WhisperLocalTranscriptionService : ITranscriptionService, IDisposable
{
    private const int SampleRate = 16000;
    private const int BytesPerSample = 2;
    private const short TargetPeak = 26000;

    private readonly WindowsSpeechTranscriptionService _fallback;
    private readonly ILogger<WhisperLocalTranscriptionService> _logger;
    private readonly WhisperLocalSettings _settings;
    private readonly SemaphoreSlim _factoryLock = new(1, 1);
    private WhisperFactory? _factory;
    private bool _disposed;

    public WhisperLocalTranscriptionService(
        WindowsSpeechTranscriptionService fallback,
        ILogger<WhisperLocalTranscriptionService> logger,
        IOptions<WhisperLocalSettings> settings)
    {
        _fallback = fallback;
        _logger = logger;
        _settings = settings.Value;
    }

    public string LastError { get; private set; } = string.Empty;

    public int TimeoutSeconds => Math.Clamp(_settings.TimeoutSeconds, 15, 180);

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await GetFactoryAsync(cancellationToken);
            LastError = string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = $"Whisper warm-up failed: {ex.Message}";
            _logger.LogWarning(ex, "Local Whisper warm-up failed. Fallback transcription remains available.");
        }
    }

    public async Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length <= 44)
        {
            LastError = "No speech audio was captured.";
            return string.Empty;
        }

        return await Task.Run(
            () => TranscribeCoreAsync(wavBytes, fileName, cancellationToken),
            cancellationToken);
    }

    private async Task<string> TranscribeCoreAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var preparedWav = PrepareSpeechWav(wavBytes);
            if (preparedWav.Length <= 44)
            {
                LastError = "No clear speech was found after audio cleanup.";
                return await RunFallbackAsync(wavBytes, fileName, cancellationToken);
            }

            var factory = await GetFactoryAsync(cancellationToken);
            var text = await RunWhisperAsync(factory, preparedWav, accurateMode: false, cancellationToken);
            if (_settings.AccurateRetryEnabled && IsSuspiciousTranscript(text, preparedWav))
            {
                text = await RunWhisperAsync(factory, preparedWav, accurateMode: true, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                LastError = string.Empty;
                return text;
            }

            LastError = "Whisper did not detect clear speech.";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = $"Whisper transcription failed: {ex.Message}";
            _logger.LogWarning(ex, "Local Whisper transcription failed. Falling back to Windows speech recognition.");
        }

        return await RunFallbackAsync(wavBytes, fileName, cancellationToken);
    }

    private async Task<string> RunWhisperAsync(
        WhisperFactory factory,
        byte[] wavBytes,
        bool accurateMode,
        CancellationToken cancellationToken)
    {
        var builder = factory.CreateBuilder()
                .WithLanguage("en")
                .WithThreads(GetThreadCount())
            .WithNoContext()
            .WithPrompt("Spoken English technical interview answer. Transcribe the candidate exactly.")
            .WithTemperature(0)
            .WithTemperatureInc(accurateMode ? 0.2f : 0)
            .WithNoSpeechThreshold(0.65f);

        if (accurateMode)
        {
            builder.WithBeamSearchSamplingStrategy(options => options.WithBeamSize(3));
        }
        else
        {
            builder.WithGreedySamplingStrategy(options => options.WithBestOf(1));
        }

        using var processor = builder.Build();
        using var stream = new MemoryStream(wavBytes);
        var transcript = new StringBuilder();

        await foreach (var result in processor.ProcessAsync(stream, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                transcript.Append(' ');
                transcript.Append(result.Text.Trim());
            }
        }

        return NormalizeTranscript(transcript.ToString());
    }

    private async Task<string> RunFallbackAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken)
    {
        var fallbackText = await _fallback.TranscribeWavAsync(wavBytes, fileName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            LastError = string.Empty;
            return fallbackText;
        }

        if (string.IsNullOrWhiteSpace(LastError))
        {
            LastError = string.IsNullOrWhiteSpace(_fallback.LastError)
                ? "Speech recognition did not return text."
                : _fallback.LastError;
        }
        else if (!string.IsNullOrWhiteSpace(_fallback.LastError))
        {
            LastError = $"{LastError} Fallback also failed: {_fallback.LastError}";
        }

        return string.Empty;
    }

    private async Task<WhisperFactory> GetFactoryAsync(CancellationToken cancellationToken)
    {
        if (_factory is not null)
        {
            return _factory;
        }

        await _factoryLock.WaitAsync(cancellationToken);
        try
        {
            if (_factory is not null)
            {
                return _factory;
            }

            var modelPath = await EnsureModelAsync(cancellationToken);
            _factory = WhisperFactory.FromPath(modelPath);
            return _factory;
        }
        finally
        {
            _factoryLock.Release();
        }
    }

    private async Task<string> EnsureModelAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ModelPath))
        {
            var configuredPath = Environment.ExpandEnvironmentVariables(_settings.ModelPath.Trim());
            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }

            throw new FileNotFoundException("Configured Whisper model file was not found.", configuredPath);
        }

        var modelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InterviewFlo",
            "Models",
            "Whisper");

        Directory.CreateDirectory(modelsDirectory);

        var modelType = ResolveModelType(_settings.Model);
        var modelPath = Path.Combine(modelsDirectory, ResolveModelFileName(modelType));
        if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 0)
        {
            return modelPath;
        }

        if (!_settings.AutoDownload)
        {
            throw new FileNotFoundException("Local Whisper model is missing and auto-download is disabled.", modelPath);
        }

        var tempPath = $"{modelPath}.download";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelType, cancellationToken: cancellationToken))
        await using (var fileWriter = File.Create(tempPath))
        {
            await modelStream.CopyToAsync(fileWriter, cancellationToken);
        }

        if (File.Exists(modelPath))
        {
            File.Delete(modelPath);
        }

        File.Move(tempPath, modelPath);
        return modelPath;
    }

    private int GetThreadCount()
    {
        var configuredMax = Math.Clamp(_settings.MaxThreads, 2, 16);
        return Math.Clamp(Environment.ProcessorCount - 1, 2, configuredMax);
    }

    private byte[] PrepareSpeechWav(byte[] wavBytes)
    {
        if (!TryReadPcm16Mono(wavBytes, out var samples, out var sampleRate) || samples.Length == 0)
        {
            return wavBytes;
        }

        var peak = samples.Max(sample => Math.Abs((int)sample));
        if (peak < 80)
        {
            return [];
        }

        var silenceThreshold = Math.Max(360, peak / 55);
        var start = 0;
        while (start < samples.Length && Math.Abs((int)samples[start]) < silenceThreshold)
        {
            start++;
        }

        var end = samples.Length - 1;
        while (end > start && Math.Abs((int)samples[end]) < silenceThreshold)
        {
            end--;
        }

        var paddingSamples = Math.Max(1, sampleRate * Math.Clamp(_settings.SilencePaddingMs, 0, 1000) / 1000);
        start = Math.Max(0, start - paddingSamples);
        end = Math.Min(samples.Length - 1, end + paddingSamples);

        var trimmedLength = end - start + 1;
        if (trimmedLength <= sampleRate * Math.Clamp(_settings.MinimumSpeechMs, 100, 3000) / 1000)
        {
            return [];
        }

        var trimmed = new short[trimmedLength];
        Array.Copy(samples, start, trimmed, 0, trimmedLength);

        var trimmedPeak = trimmed.Max(sample => Math.Abs((int)sample));
        if (trimmedPeak > 0 && trimmedPeak < TargetPeak)
        {
            var gain = Math.Min(4.0, TargetPeak / (double)trimmedPeak);
            for (var i = 0; i < trimmed.Length; i++)
            {
                var amplified = (int)Math.Round(trimmed[i] * gain);
                trimmed[i] = (short)Math.Clamp(amplified, short.MinValue, short.MaxValue);
            }
        }

        return BuildWav(trimmed, sampleRate);
    }

    private static GgmlType ResolveModelType(string? model)
    {
        return model?.Trim().ToLowerInvariant() switch
        {
            "tiny" or "tiny.en" or "tinyen" => GgmlType.TinyEn,
            "base" or "base.en" or "baseen" => GgmlType.BaseEn,
            "small" or "small.en" or "smallen" => GgmlType.SmallEn,
            "medium" or "medium.en" or "mediumen" => GgmlType.MediumEn,
            _ => GgmlType.BaseEn,
        };
    }

    private static string ResolveModelFileName(GgmlType modelType)
    {
        return modelType switch
        {
            GgmlType.TinyEn => "ggml-tiny.en.bin",
            GgmlType.BaseEn => "ggml-base.en.bin",
            GgmlType.SmallEn => "ggml-small.en.bin",
            GgmlType.MediumEn => "ggml-medium.en.bin",
            _ => "ggml-base.en.bin",
        };
    }

    private static bool TryReadPcm16Mono(byte[] wavBytes, out short[] samples, out int sampleRate)
    {
        samples = [];
        sampleRate = SampleRate;

        if (wavBytes.Length < 44 ||
            Encoding.ASCII.GetString(wavBytes, 0, 4) != "RIFF" ||
            Encoding.ASCII.GetString(wavBytes, 8, 4) != "WAVE")
        {
            return false;
        }

        var offset = 12;
        short channels = 0;
        short bitsPerSample = 0;
        var dataOffset = -1;
        var dataSize = 0;

        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = Encoding.ASCII.GetString(wavBytes, offset, 4);
            var chunkSize = BitConverter.ToInt32(wavBytes, offset + 4);
            offset += 8;

            if (chunkSize < 0 || offset + chunkSize > wavBytes.Length)
            {
                return false;
            }

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                var format = BitConverter.ToInt16(wavBytes, offset);
                channels = BitConverter.ToInt16(wavBytes, offset + 2);
                sampleRate = BitConverter.ToInt32(wavBytes, offset + 4);
                bitsPerSample = BitConverter.ToInt16(wavBytes, offset + 14);
                if (format != 1)
                {
                    return false;
                }
            }
            else if (chunkId == "data")
            {
                dataOffset = offset;
                dataSize = chunkSize;
            }

            offset += chunkSize + (chunkSize % 2);
        }

        if (dataOffset < 0 || dataSize <= 0 || bitsPerSample != 16 || channels <= 0)
        {
            return false;
        }

        var frameSize = channels * BytesPerSample;
        var frameCount = dataSize / frameSize;
        samples = new short[frameCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = dataOffset + frame * frameSize;
            var sum = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                sum += BitConverter.ToInt16(wavBytes, frameOffset + channel * BytesPerSample);
            }

            samples[frame] = (short)Math.Clamp(sum / channels, short.MinValue, short.MaxValue);
        }

        return true;
    }

    private static bool IsSuspiciousTranscript(string text, byte[] wavBytes)
    {
        if (!TryReadPcm16Mono(wavBytes, out var samples, out var sampleRate) || sampleRate <= 0)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        var durationSeconds = samples.Length / (double)sampleRate;
        var wordCount = string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return durationSeconds >= 1.4 && wordCount <= 2;
    }

    private static byte[] BuildWav(IReadOnlyList<short> samples, int sampleRate)
    {
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory);

        var dataSize = samples.Count * BytesPerSample;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * BytesPerSample);
        writer.Write((short)BytesPerSample);
        writer.Write((short)(BytesPerSample * 8));
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            writer.Write(sample);
        }

        writer.Flush();
        return memory.ToArray();
    }

    private static string NormalizeTranscript(string text)
    {
        return string.Join(' ', text
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _factory?.Dispose();
        _factoryLock.Dispose();
        _disposed = true;
    }
}
