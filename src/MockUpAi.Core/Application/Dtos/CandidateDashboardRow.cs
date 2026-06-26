using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Core.Application.Dtos;

public sealed class CandidateDashboardRow
{
    public string CandidateId { get; set; } = string.Empty;

    public string JobRole { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public InterviewCategory Category { get; set; } = InterviewCategory.Technical;

    public InterviewDifficulty Difficulty { get; set; } = InterviewDifficulty.Fresher;

    public double PassingScore { get; set; } = 60;

    public InterviewAiProvider AiProvider { get; set; } = InterviewAiProvider.OpenAi;
    public string InterviewerVoiceProfile { get; set; } = "OpenAI:Nova";

    public string InterviewStatus { get; set; } = string.Empty;

    public double LatestInterviewScore { get; set; }

    public bool IsPassed { get; set; }

    public double RoleFitScore { get; set; }

    public double TechnicalScore { get; set; }

    public double CommunicationScore { get; set; }

    public double DepthScore { get; set; }

    public double RelevanceScore { get; set; }

    public int QuestionsAnswered { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public bool IsActive { get; set; }

    public bool IsExpired { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
