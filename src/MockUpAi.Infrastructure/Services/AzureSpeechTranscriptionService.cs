using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Infrastructure.Configuration;

namespace MockUpAi.Infrastructure.Services;

internal sealed class AzureSpeechTranscriptionService : ITranscriptionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureSpeechSettings _settings;
    private readonly ILogger<AzureSpeechTranscriptionService> _logger;

    public string LastError { get; private set; } = string.Empty;

    public AzureSpeechTranscriptionService(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureSpeechSettings> settings,
        ILogger<AzureSpeechTranscriptionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length == 0)
        {
            LastError = "No audio was captured.";
            return string.Empty;
        }

        try
        {
            LastError = string.Empty;
            var key = ResolveApiKey();
            var region = _settings.Region.Trim();
            if (string.IsNullOrWhiteSpace(region))
            {
                throw new InvalidOperationException("Azure Speech region is missing.");
            }

            var language = string.IsNullOrWhiteSpace(_settings.Language) ? "en-US" : _settings.Language.Trim();
            var uri = $"https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language={Uri.EscapeDataString(language)}&format=detailed";

            var client = _httpClientFactory.CreateClient(nameof(AzureSpeechTranscriptionService));
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Add("Ocp-Apim-Subscription-Key", key);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new ByteArrayContent(wavBytes);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                LastError = string.IsNullOrWhiteSpace(errorBody)
                    ? $"Azure Speech transcription request failed: {(int)response.StatusCode} {response.StatusCode}."
                    : $"Azure Speech transcription request failed: {(int)response.StatusCode} {response.StatusCode}. {errorBody}";
                _logger.LogWarning("Azure Speech transcription failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
                return string.Empty;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                LastError = "Azure Speech returned an empty transcription response.";
                return string.Empty;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("DisplayText", out var textProp))
            {
                return textProp.GetString()?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            _logger.LogWarning(ex, "Azure Speech transcription failed.");
            return string.Empty;
        }
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return _settings.ApiKey.Trim();
        }

        var env = Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        throw new InvalidOperationException("Azure Speech API key is missing.");
    }
}
