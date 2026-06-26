namespace MockUpAi.Core.Application.Dtos;

public sealed class InterviewTurnDecision
{
    public string Acknowledgement { get; init; } = string.Empty;

    public string? ClarificationPrompt { get; init; }

    public string NextQuestion { get; init; } = string.Empty;

    public string IdealAnswerHint { get; init; } = string.Empty;

    public string Topic { get; init; } = string.Empty;

    public bool ShouldAskClarification { get; init; }

    public bool ShouldMoveToNewTopic { get; init; }

    public double TechnicalScore { get; init; }

    public double CommunicationScore { get; init; }

    public double DepthScore { get; init; }

    public double RelevanceScore { get; init; }

    public double ProblemSolvingScore { get; init; }

    public double OverallScore { get; init; }

    public string EvaluationSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> ObservedStrengths { get; init; } = [];

    public IReadOnlyList<string> ObservedGaps { get; init; } = [];
}
