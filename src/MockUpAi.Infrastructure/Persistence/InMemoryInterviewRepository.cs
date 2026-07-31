using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Infrastructure.Persistence;

internal sealed class InMemoryInterviewRepository : IInterviewRepository
{
    private readonly List<InterviewSession> _sessions = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SaveAsync(InterviewSession session, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = _sessions.FindIndex(x => x.Id == session.Id);
            if (existing >= 0)
            {
                _sessions[existing] = session;
            }
            else
            {
                _sessions.Add(session);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<InterviewSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _sessions
                .OrderByDescending(x => x.StartedAtUtc)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<InterviewSession>> GetByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _sessions
                .Where(x => x.CandidateUserId.Equals(candidateUserId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.StartedAtUtc)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, InterviewSession>> GetLatestByCandidateUserIdsAsync(
        IReadOnlyCollection<string> candidateUserIds,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var ids = candidateUserIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _sessions
                .Where(x => ids.Contains(x.CandidateUserId))
                .GroupBy(x => x.CandidateUserId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(x => x.CompletedAtUtc ?? x.StartedAtUtc)
                        .ThenByDescending(x => x.StartedAtUtc)
                        .First(),
                    StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _sessions.RemoveAll(x => x.CandidateUserId.Equals(candidateUserId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }
}
