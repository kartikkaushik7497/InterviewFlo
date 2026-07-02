namespace InterviewFlo.Core.Domain.Entities;

public sealed class InterviewQuestionResult
{
    public string QuestionId { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string IdealAnswerHint { get; set; } = string.Empty;

    public string CandidateTranscript { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public double ScoreAwarded { get; set; }

    public double TechnicalScore { get; set; }

    public double CommunicationScore { get; set; }

    public double DepthScore { get; set; }

    public double RelevanceScore { get; set; }

    public double ProblemSolvingScore { get; set; }

    public string Topic { get; set; } = string.Empty;

    public string Strengths { get; set; } = string.Empty;

    public string Gaps { get; set; } = string.Empty;

    public string Feedback { get; set; } = string.Empty;
}
