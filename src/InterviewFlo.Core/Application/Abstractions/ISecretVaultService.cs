using InterviewFlo.Core.Common;

namespace InterviewFlo.Core.Application.Abstractions;

public interface ISecretVaultService
{
    Task<string?> GetOpenAiApiKeyAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> SaveOpenAiApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<bool> HasOpenAiApiKeyAsync(CancellationToken cancellationToken = default);

    string GetKeySource();
}
