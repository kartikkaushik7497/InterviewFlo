using Microsoft.Extensions.Logging;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Infrastructure.Configuration;
using MongoDB.Driver;

namespace MockUpAi.Infrastructure.Persistence;

internal sealed class MongoInterviewRepository : IInterviewRepository
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

    public async Task DeleteByCandidateUserIdAsync(string candidateUserId, CancellationToken cancellationToken = default)
    {
        await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _interviews.DeleteManyAsync(x => x.CandidateUserId == candidateUserId, cancellationToken),
            _logger,
            "DeleteInterviewsByCandidate",
            cancellationToken);
    }
}
