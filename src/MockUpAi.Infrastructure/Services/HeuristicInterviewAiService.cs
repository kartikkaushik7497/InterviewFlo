using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Infrastructure.Services;

internal sealed class HeuristicInterviewAiService : IInterviewAiService
{
    public Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        string jobRole,
        string jobDescription,
        int count,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = string.IsNullOrWhiteSpace(jobRole) ? "General" : jobRole.Trim();
        var pool = BuildQuestionPool(normalizedRole, jobDescription);
        var chosen = pool.Take(Math.Max(1, count)).ToList();

        return Task.FromResult<IReadOnlyList<InterviewQuestion>>(chosen);
    }

    public Task<InterviewAnswerEvaluation> EvaluateAnswerAsync(
        string jobRole,
        InterviewQuestion question,
        string candidateTranscript,
        CancellationToken cancellationToken = default)
    {
        var answer = candidateTranscript?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(answer))
        {
            return Task.FromResult(new InterviewAnswerEvaluation(false, 0, "No answer captured. Please answer clearly."));
        }

        var keywords = ExtractKeywords(question.IdealAnswerHint);
        var answerWords = ExtractKeywords(answer);

        var overlap = keywords.Count == 0
            ? 0
            : keywords.Count(x => answerWords.Contains(x));

        var keywordScore = keywords.Count == 0 ? 30 : (overlap * 100.0 / keywords.Count);
        var lengthScore = Math.Min(answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 2, 30);
        var score = Math.Round(Math.Min(100, keywordScore * 0.75 + lengthScore), 2);

        var passed = score >= 55;
        var feedback = passed
            ? "Strong answer with relevant technical signals."
            : "Answer is partially relevant; add more technical depth and examples.";

        return Task.FromResult(new InterviewAnswerEvaluation(passed, score, feedback));
    }

    public Task<double> CalculateRoleFitScoreAsync(
        string jobRole,
        string jobDescription,
        IReadOnlyList<InterviewQuestionResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
        {
            return Task.FromResult(0d);
        }

        var avg = results.Average(x => x.ScoreAwarded);
        var correctnessBoost = (double)results.Count(x => x.IsCorrect) / results.Count * 20;
        var finalScore = Math.Round(Math.Min(100, avg * 0.8 + correctnessBoost), 2);

        return Task.FromResult(finalScore);
    }

    private static List<InterviewQuestion> BuildQuestionPool(string role, string jobDescription)
    {
        var roleLower = role.ToLowerInvariant();
        if (roleLower.Contains("backend") || roleLower.Contains("api"))
        {
            return
            [
                NewQuestion("Explain how you design a REST API for versioning and backward compatibility.", "Version strategy, URI or header versioning, deprecation strategy, contract compatibility."),
                NewQuestion("How would you optimize a slow database query in production?", "Use indexes, execution plans, caching, avoid N+1, monitor latency."),
                NewQuestion("What strategies do you use for secure authentication and authorization?", "JWT/session, password hashing, RBAC, least privilege, token expiry."),
                NewQuestion("Describe how you would handle distributed system failures.", "Retries, circuit breaker, idempotency, timeout, observability."),
                NewQuestion("How do you approach writing testable backend code?", "Separation of concerns, dependency injection, unit/integration tests."),
            ];
        }

        if (roleLower.Contains("frontend") || roleLower.Contains("ui"))
        {
            return
            [
                NewQuestion("How do you improve frontend performance in a large app?", "Code splitting, lazy loading, memoization, bundle analysis, caching."),
                NewQuestion("Explain component state management tradeoffs.", "Local vs global state, predictability, complexity, maintainability."),
                NewQuestion("How do you ensure accessibility in your UI?", "Semantic HTML, keyboard nav, ARIA, contrast, screen reader checks."),
                NewQuestion("How do you design responsive layouts across devices?", "Mobile-first, breakpoints, flexible grid, media queries."),
                NewQuestion("How do you handle error states and loading states for users?", "Skeletons, retries, clear messages, graceful fallbacks."),
            ];
        }

        return
        [
            NewQuestion("Tell us about a challenging project and how you solved it.", "Problem breakdown, action, collaboration, measurable impact."),
            NewQuestion("How do you prioritize tasks when requirements change?", "Communication, impact analysis, iteration planning."),
            NewQuestion("Explain your approach to debugging complex issues.", "Reproduce issue, isolate variables, logs, tools, validate fix."),
            NewQuestion("How do you ensure quality before releasing a feature?", "Testing, review, monitoring, rollback plan."),
            NewQuestion("Why are you suitable for this role based on the job description?", jobDescription),
        ];
    }

    private static InterviewQuestion NewQuestion(string prompt, string hint)
    {
        return new InterviewQuestion
        {
            Prompt = prompt,
            IdealAnswerHint = hint,
        };
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var tokens = text
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '-', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2)
            .Select(x => x.Trim());

        return tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
