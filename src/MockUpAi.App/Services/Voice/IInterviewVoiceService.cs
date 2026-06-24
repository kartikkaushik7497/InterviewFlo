namespace MockUpAi.App.Services.Voice;

public interface IInterviewVoiceService
{
    void SetVoiceProfile(string? voiceProfile);
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
}
