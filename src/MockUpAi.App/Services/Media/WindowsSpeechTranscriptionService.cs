using System.Globalization;
using System.Runtime.Versioning;
using System.Speech.Recognition;
using MockUpAi.Core.Application.Abstractions;

namespace MockUpAi.App.Services.Media;

[SupportedOSPlatform("windows")]
public sealed class WindowsSpeechTranscriptionService : ITranscriptionService
{
    private const double ReliableConfidence = 0.35;
    private const double UsableConfidence = 0.12;

    public string LastError { get; private set; } = string.Empty;

    public Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (wavBytes.Length <= 44)
        {
            LastError = "No speech audio was captured.";
            return Task.FromResult(string.Empty);
        }

        return Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastError = string.Empty;

                using var stream = new MemoryStream(wavBytes);
                using var recognizer = CreateRecognizer();
                recognizer.LoadGrammar(new DictationGrammar());
                recognizer.SetInputToWaveStream(stream);

                var text = RecognizeFullAnswer(recognizer);
                if (string.IsNullOrWhiteSpace(text))
                {
                    LastError = "Windows speech recognition did not detect clear speech. Check the selected microphone, speak closer to it, or type the answer manually.";
                }

                return text;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LastError = $"Windows offline speech recognition failed: {ex.Message}";
                return string.Empty;
            }
        }, cancellationToken);
    }

    private static SpeechRecognitionEngine CreateRecognizer()
    {
        var installed = SpeechRecognitionEngine.InstalledRecognizers();
        var englishRecognizer = installed.FirstOrDefault(x =>
            x.Culture.Equals(CultureInfo.GetCultureInfo("en-US")) ||
            x.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase));

        return englishRecognizer is null
            ? new SpeechRecognitionEngine()
            : new SpeechRecognitionEngine(englishRecognizer);
    }

    private static string RecognizeFullAnswer(SpeechRecognitionEngine recognizer)
    {
        var reliableParts = new List<string>();
        var fallbackParts = new List<string>();

        for (var i = 0; i < 12; i++)
        {
            var result = recognizer.Recognize(TimeSpan.FromSeconds(8));
            if (result is null)
            {
                break;
            }

            var candidate = SelectBestCandidate(result);
            if (candidate is null)
            {
                continue;
            }

            if (candidate.Confidence >= ReliableConfidence)
            {
                reliableParts.Add(candidate.Text);
                continue;
            }

            if (candidate.Confidence >= UsableConfidence || HasEnoughWords(candidate.Text))
            {
                fallbackParts.Add(candidate.Text);
            }
        }

        if (reliableParts.Count > 0)
        {
            return NormalizeTranscript(string.Join(' ', reliableParts));
        }

        return fallbackParts.Count > 0
            ? NormalizeTranscript(string.Join(' ', fallbackParts))
            : string.Empty;
    }

    private static TranscriptCandidate? SelectBestCandidate(RecognitionResult result)
    {
        var candidates = result.Alternates
            .Prepend(result)
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.Text.Length)
            .ToList();

        var best = candidates.FirstOrDefault();
        if (best is null)
        {
            return null;
        }

        return new TranscriptCandidate(NormalizeTranscript(best.Text), best.Confidence);
    }

    private static bool HasEnoughWords(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3;
    }

    private static string NormalizeTranscript(string text)
    {
        var normalized = string.Join(' ', text
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));

        return normalized.Trim();
    }

    private sealed record TranscriptCandidate(string Text, double Confidence);
}
