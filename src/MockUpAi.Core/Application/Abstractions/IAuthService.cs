using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Common;

namespace MockUpAi.Core.Application.Abstractions;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string userId, string password, CancellationToken cancellationToken = default);

    Task<OperationResult> CompletePasswordResetAsync(string userId, string newPassword, CancellationToken cancellationToken = default);
}
