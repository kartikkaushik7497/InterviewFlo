namespace MockUpAi.Core.Application.Dtos;

public sealed class CandidatePasswordResetRequest
{
    public string CandidateId { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;

    public bool MustChangeOnNextLogin { get; set; } = true;
}
