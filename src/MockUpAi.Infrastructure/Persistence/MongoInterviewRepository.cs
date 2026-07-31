using Microsoft.Extensions.Logging;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Infrastructure.Configuration;
using MongoDB.Driver;

namespace MockUpAi.Infrastructure.Persistence;

internal sealed class MongoInterviewRepository : IInterviewRepository, IStorageIndexInitializer
{
    private readonly IMongoCollection<InterviewSession> _interviews;
    private readonly ILogger<MongoInterviewRepository> _logger;

    public MongoInterviewRepository(IMongoDatabase database, MongoDbSettings settings, ILogger<MongoInterviewRepository> logger)
    {
        _interviews = database.GetCollection<InterviewSession>(settings.InterviewsCollectionName);
        _logger = logger;
    }

    public async Task SaveAsync(InterviewSession session, CancellationToken cancellationToken = default)
    {
        var filter = Builders<InterviewSession>.Filter.Eq(x => x.Id, session.Id);
        await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _interviews.ReplaceOneAsync(filter, session, new ReplaceOptions { IsUpsert = true }, cancellationToken),
            _logger,
            "SaveInterview",
            cancellationToken);
    }

    public async Task<IReadOnlyList<InterviewSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _interviews
                .Find(Builders<InterviewSession>.Filter.Empty)
                .SortByDescending(x => x.StartedAtUtc)
                .ToListAsync(cancellationToken),
            _logger,
            "GetAllInterviews",
            cancellationToken);
    }

    public async Task<IReadOnlyList<InterviewSession>> GetByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default)
    {
        return await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _interviews
                .Find(x => x.CandidateUserId == candidateUserId)
                .SortByDescending(x => x.StartedAtUtc)
                .ToListAsync(cancellationToken),
            _logger,
            "GetInterviewsByCandidate",
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, InterviewSession>> GetLatestByCandidateUserIdsAsync(
        IReadOnlyCollection<string> candidateUserIds,
        CancellationToken cancellationToken = default)
    {
        var ids = candidateUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, InterviewSession>(StringComparer.OrdinalIgnoreCase);
        }

        var filter = Builders<InterviewSession>.Filter.In(x => x.CandidateUserId, ids);
        var sessions = await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _interviews
                .Find(filter)
                .SortByDescending(x => x.CompletedAtUtc)
                .ThenByDescending(x => x.StartedAtUtc)
                .ToListAsync(cancellationToken),
            _logger,
            "GetLatestInterviewsByCandidates",
            cancellationToken);

        return sessions
            .GroupBy(x => x.CandidateUserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task DeleteByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default)
    {
        await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _interviews.DeleteManyAsync(x => x.CandidateUserId == candidateUserId, cancellationToken),
            _logger,
            "DeleteInterviewsByCandidate",
            cancellationToken);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = new[]
            {
                new CreateIndexModel<InterviewSession>(
                    Builders<InterviewSession>.IndexKeys
                        .Ascending(x => x.CandidateUserId)
                        .Descending(x => x.CompletedAtUtc)
                        .Descending(x => x.StartedAtUtc),
                    new CreateIndexOptions { Name = "ix_interviews_candidate_latest" }),
                new CreateIndexModel<InterviewSession>(
                    Builders<InterviewSession>.IndexKeys
                        .Descending(x => x.StartedAtUtc),
                    new CreateIndexOptions { Name = "ix_interviews_started" }),
                new CreateIndexModel<InterviewSession>(
                    Builders<InterviewSession>.IndexKeys
                        .Ascending(x => x.Status),
                    new CreateIndexOptions { Name = "ix_interviews_status" }),
            };

            await MongoRepositoryExecutor.ExecuteWithRetryAsync(
                () => _interviews.Indexes.CreateManyAsync(models, cancellationToken),
                _logger,
                "EnsureInterviewIndexes",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mongo interview index initialization failed. The app will continue without rebuilding indexes.");
        }
    }
}
