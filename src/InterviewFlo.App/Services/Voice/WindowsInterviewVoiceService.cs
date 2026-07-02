using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using Microsoft.Extensions.Options;
using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Infrastructure.Configuration;
using NAudio.Wave;

namespace InterviewFlo.App.Services.Voice;

[SupportedOSPlatform("windows")]
public sealed class WindowsInterviewVoiceService : IInterviewVoiceService
{
    private static readonly string[] PreferredWindowsVoices =
    [
        "Microsoft Heera",
        "Microsoft Ravi",
        "Microsoft Zira",
        "Microsoft Mark",
        "Microsoft David",
    ];

    private readonly SemaphoreSlim _synthGate = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ElevenLabsVoiceSettings _elevenLabsSettings;
    private readonly OpenAiSettings _openAiSettings;
    private readonly ISecretVaultService _secretVault;
    private string _voiceProfile = "OpenAI:Nova";

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
            _voiceProfile = "OpenAI:Nova";
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
            if (IsOpenAiProfile(_voiceProfile) && await TrySpeakWithOpenAiAsync(text.Trim(), cancellationToken))
            {
                return;
            }

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
            Rate = 1,
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
            requested = PreferredWindowsVoices[0];
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

    private static async Task PlayMp3Async(byte[] bytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes);
        using var mp3 = new Mp3FileReader(stream);
        using var waveOut = new WaveOutEvent();
        waveOut.Init(mp3);
        waveOut.Play();
        while (waveOut.PlaybackState == PlaybackState.Playing && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(80, cancellationToken);
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
