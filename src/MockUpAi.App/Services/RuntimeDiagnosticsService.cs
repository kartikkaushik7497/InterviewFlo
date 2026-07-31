using Microsoft.Extensions.Configuration;
using MockUpAi.Core.Application.Abstractions;

namespace MockUpAi.App.Services;

public sealed class RuntimeDiagnosticsService : IRuntimeDiagnosticsService
{
    private readonly IStorageStatusService _storageStatus;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IConfiguration _configuration;

    public RuntimeDiagnosticsService(
        IStorageStatusService storageStatus,
        ITranscriptionService transcriptionService,
        IConfiguration configuration)
    {
        _storageStatus = storageStatus;
        _transcriptionService = transcriptionService;
        _configuration = configuration;
    }

    public string GetSummary()
    {
        var transcriptionName = _transcriptionService.GetType().Name
            .Replace("TranscriptionService", string.Empty, StringComparison.OrdinalIgnoreCase);
        var aiMode = GetAiMode();
        var storageMode = _storageStatus.IsPersistent ? _storageStatus.Mode : $"{_storageStatus.Mode} temporary";

        return $"Storage: {storageMode}. Transcription: {transcriptionName}. AI: {aiMode}.";
    }

    private string GetAiMode()
    {
        if (_configuration.GetValue<bool>("OpenAi:Enabled"))
        {
            return "OpenAI configured";
        }

        if (_configuration.GetValue<bool>("Gemini:Enabled"))
        {
            return "Gemini configured";
        }

        return "Local heuristic";
    }
}
