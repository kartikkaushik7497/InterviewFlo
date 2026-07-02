namespace InterviewFlo.App.Services;

public interface IAppTelemetryService
{
    void Info(string message);

    void Error(string message, Exception? exception = null);
}
