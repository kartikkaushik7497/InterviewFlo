using System.Speech.Synthesis;

namespace MockUpAi.App.Services.Voice;

public sealed class WindowsInterviewVoiceService : IInterviewVoiceService
{
    private readonly SemaphoreSlim _synthGate = new(1, 1);

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await _synthGate.WaitAsync(cancellationToken);
        try
        {
            using var synth = new SpeechSynthesizer
            {
                Volume = 90,
                Rate = -1,
            };

            await Task.Run(() => synth.Speak(text.Trim()), cancellationToken);
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
}
