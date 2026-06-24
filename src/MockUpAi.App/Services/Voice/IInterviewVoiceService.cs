namespace MockUpAi.App.Services.Voice;

public interface IInterviewVoiceService
{
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
