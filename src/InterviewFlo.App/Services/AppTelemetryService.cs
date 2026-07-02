using Microsoft.Extensions.Logging;

namespace InterviewFlo.App.Services;

public sealed class AppTelemetryService : IAppTelemetryService
{
    private readonly ILogger<AppTelemetryService> _logger;
    private readonly string _logFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppTelemetryService(ILogger<AppTelemetryService> logger)
    {
        _logger = logger;

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewFlo", "logs");
        Directory.CreateDirectory(folder);
        _logFilePath = Path.Combine(folder, $"interviewflo_{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public void Info(string message)
    {
        _logger.LogInformation(message);
        _ = WriteAsync("INFO", message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        _logger.LogError(exception, message);
        _ = WriteAsync("ERROR", message, exception);
    }

    private async Task WriteAsync(string level, string message, Exception? exception)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception + Environment.NewLine;
        }
        else
        {
            line += Environment.NewLine;
        }

        await _gate.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line);
        }
        finally
        {
            _gate.Release();
        }
    }
}
