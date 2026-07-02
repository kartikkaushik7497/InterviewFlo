using InterviewFlo.Core.Application.Abstractions;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class NoOpTranscriptionService : ITranscriptionService
{
    public string LastError { get; private set; } = "Speech-to-text is not configured.";

    public Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default)
    {
        LastError = "Speech-to-text is not configured.";
        return Task.FromResult(string.Empty);
    }
}
