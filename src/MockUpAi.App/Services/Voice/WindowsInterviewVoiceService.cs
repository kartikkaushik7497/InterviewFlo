using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Speech.Synthesis;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Infrastructure.Configuration;
using NAudio.Wave;

namespace MockUpAi.App.Services.Voice;

[SupportedOSPlatform("windows")]
public sealed class WindowsInterviewVoiceService : IInterviewVoiceService
{
    private static readonly string[] PreferredWindowsVoices =
    [
        "Microsoft Jenny",
        "Microsoft Aria",
        "Microsoft Guy",
        "Microsoft Natasha",
        "Microsoft Clara",
        "Microsoft Heera",
        "Microsoft Ravi",
        "Microsoft Zira",
        "Microsoft Mark",
        "Microsoft David",
    ];

    private readonly SemaphoreSlim _synthGate = new(1, 1);
    private readonly object _playbackLock = new();
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ElevenLabsVoiceSettings _elevenLabsSettings;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISecretVaultService _secretVault;
    private string _voiceProfile = "Windows:Natural";
    private CancellationTokenSource _speechStopCts = new();
    private SpeechSynthesizer? _activeSynth;
    private WaveOutEvent? _activeWaveOut;

    public WindowsInterviewVoiceService(
        IHttpClientFactory httpClientFactory,
        IOptions<ElevenLabsVoiceSettings> elevenLabsSettings,
        IOptions<OpenAiSettings> openAiSettings,
        ISecretVaultService secretVault)
    {
        _httpClientFactory = httpClientFactory;
        _elevenLabsSettings = elevenLabsSettings.Value;
        _openAiSettings = openAiSettings.Value;
        _secretVault = secretVault;
    }

    public void SetVoiceProfile(string? voiceProfile)
    {
        if (string.IsNullOrWhiteSpace(voiceProfile))
        {
            _voiceProfile = "Windows:Natural";
            return;
        }

        _voiceProfile = voiceProfile.Trim();
    }

    public void Stop()
    {
        CancellationTokenSource previousCts;
        SpeechSynthesizer? activeSynth;
        WaveOutEvent? activeWaveOut;

        lock (_playbackLock)
        {
            previousCts = _speechStopCts;
            _speechStopCts = new CancellationTokenSource();
            activeSynth = _activeSynth;
            activeWaveOut = _activeWaveOut;
        }

        try
        {
            previousCts.Cancel();
        }
        catch
        {
            // Voice output is optional; cancellation should never break navigation.
        }
        finally
        {
            previousCts.Dispose();
        }

        try
        {
            activeSynth?.SpeakAsyncCancelAll();
        }
        catch
        {
            // Some Windows voices throw if cancellation races with disposal.
        }

        try
        {
            activeWaveOut?.Stop();
        }
        catch
        {
            // Audio playback may already have completed.
        }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var linkedCts = CreateSpeechCancellation(cancellationToken);
        var token = linkedCts.Token;
        var hasGate = false;

        try
        {
            await _synthGate.WaitAsync(token);
            hasGate = true;

            if (IsOpenAiProfile(_voiceProfile) && await TrySpeakWithOpenAiAsync(text.Trim(), token))
            {
                return;
            }

            if (IsElevenLabsProfile(_voiceProfile) && await TrySpeakWithElevenLabsAsync(text.Trim(), token))
            {
                return;
            }

            await SpeakWithWindowsAsync(text.Trim(), token);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user logs out or navigates away mid-sentence.
        }
        catch
        {
            // Voice output is optional; keep interview flow running.
        }
        finally
        {
            if (hasGate)
            {
                _synthGate.Release();
            }
        }
    }

    private CancellationTokenSource CreateSpeechCancellation(CancellationToken cancellationToken)
    {
        lock (_playbackLock)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _speechStopCts.Token);
        }
    }

    private static bool IsElevenLabsProfile(string profile)
    {
        return profile.StartsWith("ElevenLabs:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOpenAiProfile(string profile)
    {
        return profile.StartsWith("OpenAI:", StringComparison.OrdinalIgnoreCase) ||
               profile.StartsWith("AzureOpenAI:", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SpeakWithWindowsAsync(string text, CancellationToken cancellationToken)
    {
        using var synth = new SpeechSynthesizer
        {
            Volume = 100,
            Rate = 0,
        };

        ApplyWindowsVoiceSelection(synth, _voiceProfile);

        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SpeakCompletedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            if (args.Cancelled)
            {
                completed.TrySetCanceled(cancellationToken);
                return;
            }

            if (args.Error is not null)
            {
                completed.TrySetException(args.Error);
                return;
            }

            completed.TrySetResult();
        };

        synth.SpeakCompleted += handler;
        lock (_playbackLock)
        {
            _activeSynth = synth;
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                synth.SpeakAsyncCancelAll();
            }
            catch
            {
                // Voice may already be disposed or finished.
            }
        });

        try
        {
            try
            {
                synth.SpeakSsmlAsync(BuildNaturalSsml(text));
            }
            catch
            {
                synth.SpeakAsync(NormalizeForSpeech(text));
            }

            await completed.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            synth.SpeakCompleted -= handler;
            lock (_playbackLock)
            {
                if (ReferenceEquals(_activeSynth, synth))
                {
                    _activeSynth = null;
                }
            }
        }
    }

    private static void ApplyWindowsVoiceSelection(SpeechSynthesizer synth, string profile)
    {
        var requested = profile.StartsWith("Windows:", StringComparison.OrdinalIgnoreCase)
            ? profile["Windows:".Length..].Trim()
            : profile.Trim();

        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = PreferredWindowsVoices[0];
        }

        var installed = synth.GetInstalledVoices()
            .Select(v => v.VoiceInfo.Name)
            .ToList();

        if (IsDefaultWindowsVoiceRequest(requested))
        {
            var bestAvailable = FindBestInstalledVoice(installed);
            if (bestAvailable is not null)
            {
                synth.SelectVoice(bestAvailable);
                return;
            }
        }

        var exact = installed.FirstOrDefault(v => v.Equals(requested, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            synth.SelectVoice(exact);
            return;
        }

        var contains = installed.FirstOrDefault(v => v.Contains(requested, StringComparison.OrdinalIgnoreCase));
        if (contains is not null)
        {
            synth.SelectVoice(contains);
            return;
        }

        foreach (var preferred in PreferredWindowsVoices)
        {
            var preferredMatch = installed.FirstOrDefault(v =>
                v.Contains(preferred, StringComparison.OrdinalIgnoreCase) ||
                preferred.Contains(v, StringComparison.OrdinalIgnoreCase));
            if (preferredMatch is not null)
            {
                synth.SelectVoice(preferredMatch);
                return;
            }
        }
    }

    private static bool IsDefaultWindowsVoiceRequest(string requested)
    {
        return requested.Equals("Natural", StringComparison.OrdinalIgnoreCase) ||
               requested.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
               requested.Equals("Zira", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindBestInstalledVoice(IReadOnlyList<string> installed)
    {
        if (installed.Count == 0)
        {
            return null;
        }

        var natural = installed.FirstOrDefault(v => v.Contains("Natural", StringComparison.OrdinalIgnoreCase));
        if (natural is not null)
        {
            return natural;
        }

        foreach (var preferred in PreferredWindowsVoices)
        {
            var match = installed.FirstOrDefault(v =>
                v.Contains(preferred, StringComparison.OrdinalIgnoreCase) ||
                preferred.Contains(v, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return installed[0];
    }

    private static string BuildNaturalSsml(string text)
    {
        var normalized = NormalizeForSpeech(text);
        var chunks = Regex.Split(normalized, @"(?<=[.!?])\s+")
            .Select(chunk => chunk.Trim())
            .Where(chunk => chunk.Length > 0);

        var body = new StringBuilder();
        foreach (var chunk in chunks)
        {
            body.Append(SecurityElement.Escape(chunk));
            body.Append(chunk.EndsWith('?') ? "<break time=\"260ms\"/>" : "<break time=\"180ms\"/>");
        }

        return $"""
<speak version="1.0" xml:lang="en-US">
  <prosody rate="-4%" volume="x-loud">
    {body}
  </prosody>
</speak>
""";
    }

    private static string NormalizeForSpeech(string text)
    {
        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        normalized = normalized.Replace("AI", "A I", StringComparison.Ordinal);
        normalized = normalized.Replace("API", "A P I", StringComparison.Ordinal);
        normalized = normalized.Replace("UI", "U I", StringComparison.Ordinal);
        normalized = normalized.Replace("UX", "U X", StringComparison.Ordinal);
        return normalized;
    }

    private async Task<bool> TrySpeakWithElevenLabsAsync(string text, CancellationToken cancellationToken)
    {
        if (!_elevenLabsSettings.Enabled || string.IsNullOrWhiteSpace(_elevenLabsSettings.ApiKey))
        {
            return false;
        }

        var voiceId = ResolveElevenLabsVoiceId(_voiceProfile);
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            return false;
        }

        var client = _httpClientFactory.CreateClient(nameof(WindowsInterviewVoiceService));
        client.Timeout = TimeSpan.FromSeconds(20);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}");
        request.Headers.Add("xi-api-key", _elevenLabsSettings.ApiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        request.Content = JsonContent.Create(new
        {
            text,
            model_id = "eleven_turbo_v2_5",
            voice_settings = new { stability = 0.55, similarity_boost = 0.75 },
        });

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return false;
        }

        await PlayMp3Async(bytes, cancellationToken);

        return true;
    }

    private async Task<bool> TrySpeakWithOpenAiAsync(string text, CancellationToken cancellationToken)
    {
        var apiKey = await ResolveOpenAiApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var baseUrl = string.IsNullOrWhiteSpace(_openAiSettings.BaseUrl)
            ? "https://api.openai.com/v1"
            : _openAiSettings.BaseUrl.TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(_openAiSettings.SpeechModel)
            ? "gpt-4o-mini-tts"
            : _openAiSettings.SpeechModel.Trim();

        var client = _httpClientFactory.CreateClient(nameof(WindowsInterviewVoiceService));
        client.Timeout = TimeSpan.FromSeconds(25);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/audio/speech");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        request.Content = JsonContent.Create(new
        {
            model,
            voice = ResolveOpenAiVoice(_voiceProfile),
            input = text,
            response_format = "mp3",
            speed = 1.125,
        });

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return false;
        }

        await PlayMp3Async(bytes, cancellationToken);
        return true;
    }

    private async Task<string?> ResolveOpenAiApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_openAiSettings.ApiKey))
        {
            return _openAiSettings.ApiKey.Trim();
        }

        return await _secretVault.GetOpenAiApiKeyAsync(cancellationToken);
    }

    private static string ResolveOpenAiVoice(string profile)
    {
        var separatorIndex = profile.IndexOf(':');
        var voice = separatorIndex >= 0 ? profile[(separatorIndex + 1)..].Trim() : profile.Trim();
        return string.IsNullOrWhiteSpace(voice) ? "nova" : voice.ToLowerInvariant();
    }

    private async Task PlayMp3Async(byte[] bytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes);
        using var mp3 = new Mp3FileReader(stream);
        using var waveOut = new WaveOutEvent();
        lock (_playbackLock)
        {
            _activeWaveOut = waveOut;
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                waveOut.Stop();
            }
            catch
            {
                // Playback may already be stopped or disposed.
            }
        });

        waveOut.Init(mp3);
        waveOut.Play();
        try
        {
            while (waveOut.PlaybackState == PlaybackState.Playing && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(80, cancellationToken);
            }
        }
        finally
        {
            lock (_playbackLock)
            {
                if (ReferenceEquals(_activeWaveOut, waveOut))
                {
                    _activeWaveOut = null;
                }
            }
        }
    }

    private static string ResolveElevenLabsVoiceId(string profile)
    {
        var key = profile["ElevenLabs:".Length..].Trim();
        return key.ToLowerInvariant() switch
        {
            "patrick" => "ODq5zmih8GrVes37Dizd",
            "neal" => "VR6AewLTigWG4xSOukaG",
            _ => key,
        };
    }
}
