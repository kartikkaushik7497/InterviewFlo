using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;
using MockUpAi.Infrastructure.Persistence;

namespace MockUpAi.Tests;

public sealed class InMemoryInterviewRepositoryTests
{
    [Fact]
    public async Task GetLatestByCandidateUserIdsAsync_ReturnsNewestSessionPerCandidate()
    {
        var repository = new InMemoryInterviewRepository();
        var older = new InterviewSession
        {
            Id = "older",
            CandidateUserId = "kartik",
            StartedAtUtc = DateTime.UtcNow.AddHours(-2),
            CompletedAtUtc = DateTime.UtcNow.AddHours(-1),
            Status = InterviewStatus.Completed,
            OverallScore = 42,
        };
        var newer = new InterviewSession
        {
            Id = "newer",
            CandidateUserId = "kartik",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Status = InterviewStatus.Completed,
            OverallScore = 88,
        };
        var other = new InterviewSession
        {
            Id = "other",
            CandidateUserId = "demo",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = InterviewStatus.InProgress,
            OverallScore = 12,
        };

        await repository.SaveAsync(older);
        await repository.SaveAsync(newer);
        await repository.SaveAsync(other);

        var latest = await repository.GetLatestByCandidateUserIdsAsync(["kartik", "demo"]);

        Assert.Equal("newer", latest["kartik"].Id);
        Assert.Equal("other", latest["demo"].Id);
    }
}
