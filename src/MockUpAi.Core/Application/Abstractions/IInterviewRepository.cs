using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Core.Application.Abstractions;

public interface IInterviewRepository
{
    Task SaveAsync(InterviewSession session, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InterviewSession>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InterviewSession>> GetByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default);

    Task DeleteByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default);
}
