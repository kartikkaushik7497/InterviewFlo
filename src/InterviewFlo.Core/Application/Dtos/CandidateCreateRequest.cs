using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Core.Application.Dtos;

public sealed class CandidateCreateRequest
{
    public string CandidateId { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string JobRole { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public InterviewCategory Category { get; set; } = InterviewCategory.Technical;

    public InterviewDifficulty Difficulty { get; set; } = InterviewDifficulty.Fresher;

    public double PassingScore { get; set; } = 60;

    public InterviewAiProvider AiProvider { get; set; } = InterviewAiProvider.OpenAi;
    public string InterviewerVoiceProfile { get; set; } = "OpenAI:Nova";

    public DateTime? ExpiresAtUtc { get; set; }

    public bool MustChangePasswordOnFirstLogin { get; set; } = true;
}
