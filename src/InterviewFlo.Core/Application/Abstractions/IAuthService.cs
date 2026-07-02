using InterviewFlo.Core.Application.Dtos;
using InterviewFlo.Core.Common;

namespace InterviewFlo.Core.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string userId, string password, CancellationToken cancellationToken = default);

    Task<OperationResult> CompletePasswordResetAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
}
