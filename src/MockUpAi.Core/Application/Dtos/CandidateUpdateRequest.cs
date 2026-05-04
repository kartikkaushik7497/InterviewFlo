namespace MockUpAi.Core.Application.Dtos;

public sealed class CandidateUpdateRequest
{
    public string CandidateId { get; set; } = string.Empty;

    public string JobRole { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public DateTime? ExpiresAtUtc { get; set; }
}
