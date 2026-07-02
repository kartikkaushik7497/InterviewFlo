using Microsoft.Extensions.Logging;
using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;
using InterviewFlo.Infrastructure.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace InterviewFlo.Infrastructure.Persistence;

internal sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<AppUser> _users;
    private readonly ILogger<MongoUserRepository> _logger;

    public MongoUserRepository(IMongoDatabase database, MongoDbSettings settings, ILogger<MongoUserRepository> logger)
    {
        _users = database.GetCollection<AppUser>(settings.UsersCollectionName);
        _logger = logger;
    }

    public async Task SeedAdminAsync(string adminUserId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var normalizedAdmin = NormalizeUserId(adminUserId);
        var existing = await GetByUserIdAsync(normalizedAdmin, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserId = normalizedAdmin,
            PasswordHash = passwordHash,
            ActorType = UserActorType.Admin,
            JobRole = "Platform Administrator",
            JobDescription = "Controls platform operations and interview governance.",
            MustChangePassword = false,
            PasswordChangedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        await AddAsync(admin, cancellationToken);
    }

    public async Task<AppUser?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUserId(userId);
        var filter = UserIdEqualsFilter(normalized);
        return await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _users.Find(filter).FirstOrDefaultAsync(cancellationToken),
            _logger,
            "GetByUserId",
            cancellationToken);
    }

    public async Task<bool> CandidateExistsAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUserId(candidateId);
        var filter = UserIdEqualsFilter(normalized);
        var count = await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _users.CountDocumentsAsync(filter, cancellationToken: cancellationToken),
            _logger,
            "CandidateExists",
            cancellationToken);
        return count > 0;
    }

    public async Task AddAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        user.UserId = NormalizeUserId(user.UserId);

        await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _users.InsertOneAsync(user, cancellationToken: cancellationToken),
            _logger,
            "AddUser",
            cancellationToken);
    }

    public async Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        user.UserId = NormalizeUserId(user.UserId);

        await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _users.ReplaceOneAsync(x => x.Id == user.Id, user, new ReplaceOptions { IsUpsert = false }, cancellationToken),
            _logger,
            "UpdateUser",
            cancellationToken);
    }

    public async Task DeleteCandidateAsync(string candidateId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeUserId(candidateId);

        await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _users.DeleteOneAsync(x => x.UserId == normalized && x.ActorType == UserActorType.Candidate, cancellationToken),
            _logger,
            "DeleteCandidate",
            cancellationToken);
    }

    public async Task<IReadOnlyList<AppUser>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var users = await MongoRepositoryExecutor.ExecuteWithRetryAsync(
            () => _users
                .Find(x => x.ActorType == UserActorType.Candidate)
                .SortByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken),
            _logger,
            "GetCandidates",
            cancellationToken);

        return users;
    }

    private static string NormalizeUserId(string userId)
    {
        return userId.Trim().ToLowerInvariant();
    }

    private static FilterDefinition<AppUser> UserIdEqualsFilter(string normalizedUserId)
    {
        return Builders<AppUser>.Filter.Regex(
            nameof(AppUser.UserId),
            new BsonRegularExpression($"^{Regex.Escape(normalizedUserId)}$", "i"));
    }
}
