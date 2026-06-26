using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Core.Domain.Entities;

public sealed class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string UserId { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserActorType ActorType { get; set; } = UserActorType.Candidate;

    public string JobRole { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public InterviewCategory InterviewCategory { get; set; } = InterviewCategory.Technical;

    public InterviewDifficulty InterviewDifficulty { get; set; } = InterviewDifficulty.Fresher;

    public double PassingScore { get; set; } = 60;

    public InterviewAiProvider AiProvider { get; set; } = InterviewAiProvider.OpenAi;
    public string InterviewerVoiceProfile { get; set; } = "OpenAI:Nova";

    public bool IsActive { get; set; } = true;

    public DateTime? ExpiresAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime PasswordChangedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
