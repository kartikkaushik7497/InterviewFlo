namespace MockUpAi.Infrastructure.Services;

public interface IStartupSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
