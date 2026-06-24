using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Speech.Synthesis;
using Microsoft.Extensions.Options;
using NAudio.Wave;

namespace MockUpAi.App.Services.Voice;

public sealed class WindowsInterviewVoiceService : IInterviewVoiceService
{
    private readonly SemaphoreSlim _synthGate = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ElevenLabsVoiceSettings _elevenLabsSettings;
    private string _voiceProfile = "Windows:David";

    public WindowsInterviewVoiceService(
        IHttpClientFactory httpClientFactory,
        IOptions<ElevenLabsVoiceSettings> elevenLabsSettings)
    {
        _httpClientFactory = httpClientFactory;
        _elevenLabsSettings = elevenLabsSettings.Value;
    }

    public void SetVoiceProfile(string? voiceProfile)
    {
        if (string.IsNullOrWhiteSpace(voiceProfile))
        {
            _voiceProfile = "Windows:David";
            return;
        }

        _voiceProfile = voiceProfile.Trim();
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _synthGate.WaitAsync(cancellationToken);
        try
        {
            if (IsElevenLabsProfile(_voiceProfile) && await TrySpeakWithElevenLabsAsync(text.Trim(), cancellationToken))
            {
                return;
            }

            await SpeakWithWindowsAsync(text.Trim(), cancellationToken);
        }
        catch
        {
            // Voice output is optional; keep interview flow running.
        }
        finally
        {
            _synthGate.Release();
        }
    }

    private static bool IsElevenLabsProfile(string profile)
    {
        return profile.StartsWith("ElevenLabs:", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SpeakWithWindowsAsync(string text, CancellationToken cancellationToken)
    {
        using var synth = new SpeechSynthesizer
        {
            Volume = 95,
            Rate = -1,
        };

        ApplyWindowsVoiceSelection(synth, _voiceProfile);
        await Task.Run(() => synth.Speak(text), cancellationToken);
    }

    private static void ApplyWindowsVoiceSelection(SpeechSynthesizer synth, string profile)
    {
        var requested = profile.StartsWith("Windows:", StringComparison.OrdinalIgnoreCase)
            ? profile["Windows:".Length..].Trim()
            : profile.Trim();

        if (string.IsNullOrWhiteSpace(requested))
        {
            requested = "David";
        }

        var installed = synth.GetInstalledVoices()
            .Select(v => v.VoiceInfo.Name)
            .ToList();

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
        }
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

        using var stream = new MemoryStream(bytes);
        using var mp3 = new Mp3FileReader(stream);
        using var waveOut = new WaveOutEvent();
        waveOut.Init(mp3);
        waveOut.Play();
        while (waveOut.PlaybackState == PlaybackState.Playing && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(80, cancellationToken);
        }

        return true;
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
