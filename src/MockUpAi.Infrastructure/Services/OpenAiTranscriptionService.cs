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

    public string LastError { get; private set; } = string.Empty;

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
            LastError = "No audio was captured.";
            return string.Empty;
        }

        try
        {
            LastError = string.Empty;
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
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                LastError = BuildHttpError(response.StatusCode, errorBody);
                _logger.LogWarning("OpenAI transcription failed with {StatusCode}: {Body}", response.StatusCode, errorBody);
                return string.Empty;
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                LastError = "OpenAI returned an empty transcription.";
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
            LastError = ex.Message;
            _logger.LogWarning(ex, "Transcription failed.");
            return string.Empty;
        }
    }

    private static string BuildHttpError(System.Net.HttpStatusCode statusCode, string errorBody)
    {
        var status = $"{(int)statusCode} {statusCode}";
        var message = TryReadOpenAiErrorMessage(errorBody);

        return string.IsNullOrWhiteSpace(message)
            ? $"OpenAI transcription request failed: {status}."
            : $"OpenAI transcription request failed: {status}. {message}";
    }

    private static string TryReadOpenAiErrorMessage(string errorBody)
    {
        if (string.IsNullOrWhiteSpace(errorBody))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(errorBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return errorBody.Length > 220 ? errorBody[..220] : errorBody;
        }

        return string.Empty;
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
