using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MockUpAi.Infrastructure.Persistence;

internal static class MongoRepositoryExecutor
{
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delayMs = 250 * attempt;
                logger.LogWarning(ex, "Transient Mongo error during {Operation}. Retrying attempt {Attempt}/{MaxAttempts} after {DelayMs}ms.", operationName, attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return await operation();
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delayMs = 250 * attempt;
                logger.LogWarning(ex, "Transient Mongo error during {Operation}. Retrying attempt {Attempt}/{MaxAttempts} after {DelayMs}ms.", operationName, attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        await operation();
    }

    private static bool IsTransient(Exception ex)
    {
        return ex is MongoConnectionException ||
               ex is MongoExecutionTimeoutException ||
               ex is TimeoutException ||
               ex is MongoNotPrimaryException ||
               ex is MongoNodeIsRecoveringException;
    }
}
