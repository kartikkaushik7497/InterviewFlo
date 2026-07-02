namespace InterviewFlo.Core.Application.Abstractions;

public interface IStorageStatusService
{
    string Mode { get; }

    bool IsPersistent { get; }

    string Message { get; }
}
