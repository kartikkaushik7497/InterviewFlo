using InterviewFlo.Core.Domain.Entities;

namespace InterviewFlo.Core.Application.Abstractions;

public interface IUserRepository
{
    Task SeedAdminAsync(string adminUserId, string passwordHash, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> CandidateExistsAsync(string candidateId, CancellationToken cancellationToken = default);

    Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

    Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default);

    Task DeleteCandidateAsync(string candidateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppUser>> GetCandidatesAsync(CancellationToken cancellationToken = default);
}
