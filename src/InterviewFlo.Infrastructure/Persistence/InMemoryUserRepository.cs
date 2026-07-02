using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Infrastructure.Persistence;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<AppUser> _users = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SeedAdminAsync(string adminUserId, string passwordHash, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var exists = _users.Any(x => x.UserId.Equals(adminUserId, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                return;
            }

            _users.Add(new AppUser
            {
                UserId = adminUserId,
                PasswordHash = passwordHash,
                ActorType = UserActorType.Admin,
                JobRole = "Platform Administrator",
                JobDescription = "Controls platform operations and interview governance.",
                MustChangePassword = false,
                PasswordChangedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppUser?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _users.FirstOrDefault(x => x.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CandidateExistsAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _users.Any(x => x.UserId.Equals(candidateId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _users.Add(user);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var index = _users.FindIndex(x => x.Id == user.Id);
            if (index >= 0)
            {
                _users[index] = user;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteCandidateAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _users.RemoveAll(x => x.ActorType == UserActorType.Candidate && x.UserId.Equals(candidateId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AppUser>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _users
                .Where(x => x.ActorType == UserActorType.Candidate)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }
}
