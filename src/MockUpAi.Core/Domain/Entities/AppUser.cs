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

    public bool IsActive { get; set; } = true;

    public DateTime? ExpiresAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime PasswordChangedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
