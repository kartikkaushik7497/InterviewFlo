using Microsoft.Extensions.Configuration;
using MockUpAi.Core.Application.Abstractions;

namespace MockUpAi.Infrastructure.Services;

internal sealed class StartupSeeder : IStartupSeeder
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _configuration;

    public StartupSeeder(IUserRepository users, IConfiguration configuration)
    {
        _users = users;
        _configuration = configuration;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var adminUserId = _configuration["SeedAdmin:UserId"] ?? "admin";
        var adminPassword = _configuration["SeedAdmin:Password"] ??
                            Environment.GetEnvironmentVariable("MOCKUPAI_SEED_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "SeedAdmin:Password or MOCKUPAI_SEED_ADMIN_PASSWORD must be configured to seed the first admin account.");
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(adminPassword);

        await _users.SeedAdminAsync(adminUserId, hash, cancellationToken);
    }
}
