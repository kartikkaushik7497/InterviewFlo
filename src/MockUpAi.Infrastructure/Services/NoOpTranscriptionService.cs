using MockUpAi.Core.Application.Abstractions;

namespace MockUpAi.Infrastructure.Services;

internal sealed class NoOpTranscriptionService : ITranscriptionService
{
    public Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}
