namespace MockUpAi.Core.Application.Dtos;

public sealed class CandidateLifecycleUpdateRequest
{
    public string CandidateId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }
}
