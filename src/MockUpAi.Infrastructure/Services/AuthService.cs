using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Common;
using MockUpAi.Core.Domain.Entities;
using MongoDB.Driver;

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

        var lookup = await GetUserForLoginAsync(userId.Trim(), cancellationToken);
        if (lookup.StorageUnavailableMessage is not null)
        {
            return new AuthResult(false, lookup.StorageUnavailableMessage, null, false);
        }

        var user = lookup.User;
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
        try
        {
            await _users.UpdateAsync(user, cancellationToken);
        }
        catch (Exception ex) when (IsStorageUnavailable(ex))
        {
            // Last-login tracking should not block an otherwise verified login.
        }

        if (user.MustChangePassword)
        {
            return new AuthResult(true, "Password reset required.", user, true);
        }

        return new AuthResult(true, "Login successful.", user, false);
    }

    public async Task<OperationResult> CompletePasswordResetAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
    {
        AppUser? user;
        try
        {
            user = await _users.GetByUserIdAsync(userId, cancellationToken);
        }
        catch (Exception ex) when (IsStorageUnavailable(ex))
        {
            return OperationResult.Failure(BuildStorageUnavailableMessage());
        }

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

        try
        {
            await _users.UpdateAsync(user, cancellationToken);
        }
        catch (Exception ex) when (IsStorageUnavailable(ex))
        {
            return OperationResult.Failure(BuildStorageUnavailableMessage());
        }

        return OperationResult.Success("Password updated successfully.");
    }

    private async Task<(AppUser? User, string? StorageUnavailableMessage)> GetUserForLoginAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _users.GetByUserIdAsync(userId, cancellationToken), null);
        }
        catch (Exception ex) when (IsStorageUnavailable(ex))
        {
            return (null, BuildStorageUnavailableMessage());
        }
    }

    private static bool IsStorageUnavailable(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is MongoConnectionException ||
                current is MongoExecutionTimeoutException ||
                current is MongoWaitQueueFullException ||
                current is TimeoutException)
            {
                return true;
            }

            if (current is InvalidOperationException &&
                current.Message.Contains("MongoDB is reconnecting", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildStorageUnavailableMessage()
    {
        return "MongoDB is reconnecting or temporarily unreachable. Wait a few seconds and press Login again; restarting the app should not be necessary.";
    }
}
