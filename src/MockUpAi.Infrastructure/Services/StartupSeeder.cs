using Microsoft.Extensions.Configuration;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Infrastructure.Persistence;

namespace MockUpAi.Infrastructure.Services;

internal sealed class StartupSeeder : IStartupSeeder
{
    private readonly IUserRepository _users;
    private readonly IInterviewRepository _interviews;
    private readonly IConfiguration _configuration;

    public StartupSeeder(IUserRepository users, IInterviewRepository interviews, IConfiguration configuration)
    {
        _users = users;
        _interviews = interviews;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureIndexesIfSupportedAsync(_users, cancellationToken);
        await EnsureIndexesIfSupportedAsync(_interviews, cancellationToken);

        var adminUserId = _configuration["SeedAdmin:UserId"] ?? "admin";
        var configuredAdminPassword = _configuration["SeedAdmin:Password"];
        var adminPassword = string.IsNullOrWhiteSpace(configuredAdminPassword)
            ? Environment.GetEnvironmentVariable("MOCKUPAI_SEED_ADMIN_PASSWORD")
            : configuredAdminPassword;

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "SeedAdmin:Password or MOCKUPAI_SEED_ADMIN_PASSWORD must be configured to seed the first admin account.");
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword);

        await _users.SeedAdminAsync(adminUserId, hash, cancellationToken);
    }

    private static async Task EnsureIndexesIfSupportedAsync(object repository, CancellationToken cancellationToken)
    {
        if (repository is IStorageIndexInitializer initializer)
        {
            await initializer.EnsureIndexesAsync(cancellationToken);
        }
    }
}
