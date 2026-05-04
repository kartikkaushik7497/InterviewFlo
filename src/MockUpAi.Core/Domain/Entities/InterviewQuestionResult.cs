namespace MockUpAi.Core.Domain.Entities;

public sealed class InterviewQuestionResult
{
    public string QuestionId { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string IdealAnswerHint { get; set; } = string.Empty;

    public string CandidateTranscript { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public double ScoreAwarded { get; set; }

    public string Feedback { get; set; } = string.Empty;
}
