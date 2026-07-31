using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Core.Domain.Entities;

public sealed class InterviewSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string CandidateUserId { get; set; } = string.Empty;

    public string JobRole { get; set; } = string.Empty;

    public string JobDescription { get; set; } = string.Empty;

    public InterviewCategory Category { get; set; } = InterviewCategory.Technical;

    public InterviewDifficulty Difficulty { get; set; } = InterviewDifficulty.Fresher;

    public double PassingScore { get; set; } = 60;

    public InterviewAiProvider AiProvider { get; set; } = InterviewAiProvider.Heuristic;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public InterviewStatus Status { get; set; } = InterviewStatus.Pending;

    public InterviewFlowState FlowState { get; set; } = InterviewFlowState.Intro;

    public List<InterviewQuestionResult> QuestionResults { get; set; } = [];

    public List<InterviewConversationTurn> ConversationTurns { get; set; } = [];

    public int PlannedQuestionCount { get; set; } = 12;

    public double OverallScore { get; set; }

    public double RoleFitScore { get; set; }

    public bool IsPassed { get; set; }

    public int FeedbackRating { get; set; }

    public string FeedbackSuggestions { get; set; } = string.Empty;

    public DateTime? FeedbackSubmittedAtUtc { get; set; }
}
