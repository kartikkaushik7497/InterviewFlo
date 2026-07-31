namespace MockUpAi.Infrastructure.Persistence;

internal interface IStorageIndexInitializer
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}
