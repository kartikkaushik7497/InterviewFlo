using InterviewFlo.Core.Application.Abstractions;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class StorageStatusService : IStorageStatusService
{
    public StorageStatusService(string mode, bool isPersistent, string message)
    {
        Mode = mode;
        IsPersistent = isPersistent;
        Message = message;
    }

    public string Mode { get; }

    public bool IsPersistent { get; }

    public string Message { get; }
}
