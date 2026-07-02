using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Application.Dtos;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class HeuristicInterviewTurnOrchestrator
{
    public Task<InterviewTurnDecision> DecideNextTurnAsync(
        InterviewSession session,
        InterviewQuestion currentQuestion,
        string candidateAnswer,
        int currentQuestionIndex,
        int totalQuestionCount,
        CancellationToken cancellationToken = default)
    {
        var answer = candidateAnswer.Trim();
        var words = answer.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;
        var isWeak = wordCount < 18 || IsLowQuality(answer);
        var hintKeywords = Keywords(currentQuestion.IdealAnswerHint);
        var answerKeywords = Keywords(answer);
        var overlap = hintKeywords.Count == 0 ? 0d : hintKeywords.Count(answerKeywords.Contains) * 100.0 / hintKeywords.Count;

        var relevance = Math.Clamp(overlap * 0.7 + Math.Min(wordCount, 60) * 0.5, 0d, 100d);
        var depth = Math.Clamp(wordCount * 1.5 + overlap * 0.35, 0d, 100d);
        var communication = IsLowQuality(answer) ? 10d : Math.Clamp(35d + Math.Min(wordCount, 60), 0d, 100d);
        var technical = Math.Clamp(overlap * 0.9 + depth * 0.25, 0d, 100d);
        var problemSolving = Math.Clamp(depth * 0.75 + relevance * 0.25, 0d, 100d);
        var overall = Weighted(technical, depth, relevance, communication, problemSolving);

        var nextTopic = NextTopic(session, currentQuestionIndex);
        var shouldClarify = isWeak && currentQuestionIndex < totalQuestionCount - 1;
        var nextQuestion = shouldClarify
            ? $"Could you make that more concrete? Please give one specific example, your action, and the result."
            : BuildNextQuestion(session.JobRole, session.Difficulty, nextTopic);

        return Task.FromResult(new InterviewTurnDecision
        {
            Acknowledgement = BuildAcknowledgement(overall, isWeak),
            ClarificationPrompt = shouldClarify ? nextQuestion : null,
            NextQuestion = nextQuestion,
            IdealAnswerHint = shouldClarify
                ? "Concrete project context, candidate action, technical detail, measurable result."
                : $"Role-specific evidence for {nextTopic}; practical decisions, tradeoffs, and outcome.",
            Topic = nextTopic,
            ShouldAskClarification = shouldClarify,
            ShouldMoveToNewTopic = !shouldClarify,
            TechnicalScore = Math.Round(technical, 2),
            CommunicationScore = Math.Round(communication, 2),
            DepthScore = Math.Round(depth, 2),
            RelevanceScore = Math.Round(relevance, 2),
            ProblemSolvingScore = Math.Round(problemSolving, 2),
            OverallScore = Math.Round(overall, 2),
            EvaluationSummary = isWeak
                ? "The answer was too brief or unclear and needs concrete evidence."
                : "The answer provided some useful signal; continue probing for role-specific depth.",
            ObservedStrengths = overall >= 60 ? ["Some relevant answer signal"] : [],
            ObservedGaps = isWeak ? ["Needs a concrete example", "Needs more technical depth"] : ["Probe for tradeoffs and measurable impact"],
        });
    }

    internal static double Weighted(double technical, double depth, double relevance, double communication, double problemSolving)
    {
        return Math.Clamp(
            technical * 0.35 +
            depth * 0.25 +
            relevance * 0.15 +
            communication * 0.15 +
            problemSolving * 0.10,
            0,
            100);
    }

    private static string BuildAcknowledgement(double score, bool isWeak)
    {
        if (isWeak)
        {
            return "Thanks. I need a bit more detail to evaluate that fairly.";
        }

        return score >= 70
            ? "Thanks, that gives me a useful signal."
            : "Thanks. Let us tighten that with a more specific example.";
    }

    private static string BuildNextQuestion(string role, InterviewDifficulty difficulty, string topic)
    {
        var depth = difficulty == InterviewDifficulty.Fresher
            ? "at a practical beginner level"
            : "including tradeoffs, failure cases, and implementation details";
        return $"Let us move to {topic}. For the {role} role, how would you handle this {depth}?";
    }

    private static string NextTopic(InterviewSession session, int index)
    {
        var role = session.JobRole.ToLowerInvariant();
        var topics = role.Contains("backend")
            ? new[] { "API design", "database performance", "authentication and security", "testing and reliability", "production debugging" }
            : role.Contains("frontend")
                ? ["component design", "state management", "performance", "accessibility", "error handling"]
                : ["project execution", "problem solving", "technical fundamentals", "collaboration", "quality and delivery"];

        return topics[Math.Clamp(index + 1, 0, topics.Length - 1)];
    }

    private static bool IsLowQuality(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return true;
        }

        var normalized = answer.Trim().ToLowerInvariant();
        return normalized is "skip" or "pass" or "i don't know" or "i dont know" ||
               normalized.Contains("fuck", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> Keywords(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '-', '_', '\n', '\r', '\t', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
