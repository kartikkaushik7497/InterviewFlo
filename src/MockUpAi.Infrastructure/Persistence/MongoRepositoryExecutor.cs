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
        const int maxAttempts = 5;
        Exception? lastTransientException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                lastTransientException = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }

                var delayMs = GetRetryDelayMs(attempt);
                logger.LogWarning(ex, "Transient Mongo error during {Operation}. Retrying attempt {Attempt}/{MaxAttempts} after {DelayMs}ms.", operationName, attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        throw CreateUnavailableException(operationName, lastTransientException);
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 5;
        Exception? lastTransientException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                lastTransientException = ex;
                if (attempt == maxAttempts)
                {
                    break;
                }

                var delayMs = GetRetryDelayMs(attempt);
                logger.LogWarning(ex, "Transient Mongo error during {Operation}. Retrying attempt {Attempt}/{MaxAttempts} after {DelayMs}ms.", operationName, attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        throw CreateUnavailableException(operationName, lastTransientException);
    }

    private static bool IsTransient(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is MongoConnectionException ||
                current is MongoExecutionTimeoutException ||
                current is TimeoutException ||
                current is MongoWaitQueueFullException ||
                current is MongoNotPrimaryException ||
                current is MongoNodeIsRecoveringException)
            {
                return true;
            }

            if (current is MongoException mongoException &&
                (mongoException.HasErrorLabel("RetryableWriteError") ||
                 mongoException.HasErrorLabel("TransientTransactionError") ||
                 mongoException.HasErrorLabel("UnknownTransactionCommitResult")))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetRetryDelayMs(int attempt)
    {
        return attempt switch
        {
            1 => 350,
            2 => 800,
            3 => 1_500,
            _ => 2_500,
        };
    }

    private static InvalidOperationException CreateUnavailableException(string operationName, Exception? innerException)
    {
        return new InvalidOperationException(
            $"MongoDB is reconnecting or temporarily unreachable during {operationName}. Please wait a few seconds and try again; no app restart should be needed.",
            innerException);
    }
}
