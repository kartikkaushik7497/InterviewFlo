using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Common;

namespace MockUpAi.Infrastructure.Services;

internal sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordPolicyService _passwordPolicy;

    public AuthService(IUserRepository users, IPasswordPolicyService passwordPolicy)
    {
        _users = users;
        _passwordPolicy = passwordPolicy;
    }

    public async Task<AuthResult> LoginAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            return new AuthResult(false, "Enter both user id and password.", null, false);
        }

        var user = await _users.GetByUserIdAsync(userId.Trim(), cancellationToken);
        if (user is null)
        {
            return new AuthResult(false, "Account not found.", null, false);
        }

        if (!user.IsActive)
        {
            return new AuthResult(false, "Account is inactive. Contact admin.", null, false);
        }

        if (user.ExpiresAtUtc.HasValue && user.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new AuthResult(false, "Account has expired. Contact admin.", null, false);
        }

        var isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!isValid)
        {
            return new AuthResult(false, "Invalid password.", null, false);
        }

        user.LastLoginAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _users.UpdateAsync(user, cancellationToken);

        if (user.MustChangePassword)
        {
            return new AuthResult(true, "Password reset required.", user, true);
        }

        return new AuthResult(true, "Login successful.", user, false);
    }

    public async Task<OperationResult> CompletePasswordResetAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return OperationResult.Failure("User not found.");
        }

        if (!_passwordPolicy.IsValid(newPassword, out var error))
        {
            return OperationResult.Failure(error);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.MustChangePassword = false;
        user.PasswordChangedAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _users.UpdateAsync(user, cancellationToken);
        return OperationResult.Success("Password updated successfully.");
    }
}
