namespace InterviewFlo.Core.Application.Dtos;

public sealed class AdminPasswordRotateRequest
{
    public string AdminUserId { get; set; } = string.Empty;

    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
