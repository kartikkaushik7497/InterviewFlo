namespace MockUpAi.Core.Application.Abstractions;

public interface ITranscriptionService
{
    Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default);
}
