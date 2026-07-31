namespace MockUpAi.App.Services.Voice;

public interface IInterviewVoiceService
{
    void SetVoiceProfile(string? voiceProfile);
    void Stop();
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
