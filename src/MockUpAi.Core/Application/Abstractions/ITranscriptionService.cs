namespace MockUpAi.Core.Application.Abstractions;

public interface ITranscriptionService
{
    string LastError { get; }

    Task<string> TranscribeWavAsync(byte[] wavBytes, string fileName, CancellationToken cancellationToken = default);
}
