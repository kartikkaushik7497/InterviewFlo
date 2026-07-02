using InterviewFlo.Core.Domain.Entities;

namespace InterviewFlo.App.Services;

public sealed class SessionContext
{
    public AppUser? CurrentUser { get; set; }

    public InterviewSession? LastInterview { get; set; }

    public bool RequiresPasswordReset { get; set; }

    public string PendingPasswordResetUserId { get; set; } = string.Empty;

    public bool RulesAcknowledged { get; set; }

    public DateTime? RulesAcknowledgedAtUtc { get; set; }

    public void Clear()
    {
        CurrentUser = null;
        LastInterview = null;
        RequiresPasswordReset = false;
        PendingPasswordResetUserId = string.Empty;
        RulesAcknowledged = false;
        RulesAcknowledgedAtUtc = null;
    }
}
