using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Infrastructure.Configuration;

namespace MockUpAi.Infrastructure.Services;

internal sealed class OpenAiTranscriptionService : ITranscriptionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiSettings _settings;
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<OpenAiTranscriptionService> _logger;

    public OpenAiTranscriptionService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiSettings> settings,
        ISecretVaultService secretVault,
        ILogger<OpenAiTranscriptionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _secretVault = secretVault;
        _logger = logger;
    }

    public async Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var key = await ResolveApiKeyAsync(cancellationToken);
            var client = _httpClientFactory.CreateClient(nameof(OpenAiTranscriptionService));
            client.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

            using var form = new MultipartFormDataContent();
            using var audioContent = new ByteArrayContent(wavBytes);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

            form.Add(new StringContent(_settings.TranscriptionModel), "model");
            form.Add(new StringContent("text"), "response_format");
            form.Add(audioContent, "file", fileName);

            using var response = await client.PostAsync("audio/transcriptions", form, cancellationToken);
            response.EnsureSuccessStatusCode();

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (text.TrimStart().StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString() ?? string.Empty;
                }
            }

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transcription failed.");
            return string.Empty;
        }
    }

    private async Task<string> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return _settings.ApiKey;
        }

        var key = await _secretVault.GetOpenAiApiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        throw new InvalidOperationException("OpenAI API key is missing.");
    }
}
