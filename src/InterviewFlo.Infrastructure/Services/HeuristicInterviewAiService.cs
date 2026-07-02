using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class HeuristicInterviewAiService : IInterviewAiService
{
    public Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        string jobDescription,
        InterviewDifficulty difficulty,
        int count,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = string.IsNullOrWhiteSpace(jobRole) ? "General" : jobRole.Trim();
        var pool = BuildQuestionPool(normalizedRole, category, jobDescription, difficulty);
        var chosen = pool.Take(Math.Max(1, count)).ToList();

        return Task.FromResult<IReadOnlyList<InterviewQuestion>>(chosen);
    }

    public Task<InterviewAnswerEvaluation> EvaluateAnswerAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewDifficulty difficulty,
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
        var strictness = difficulty switch
        {
            InterviewDifficulty.Professional => 1.0,
            InterviewDifficulty.Experienced => 0.95,
            _ => 0.9,
        };

        var score = Math.Round(Math.Min(100, (keywordScore * 0.75 + lengthScore) * strictness), 2);
        var passCutoff = difficulty switch
        {
            InterviewDifficulty.Professional => 68,
            InterviewDifficulty.Experienced => 60,
            _ => 52,
        };

        var passed = score >= passCutoff;
        var feedback = passed
            ? "Strong answer with relevant technical signals."
            : "Answer is partially relevant; add more technical depth and examples.";

        return Task.FromResult(new InterviewAnswerEvaluation(passed, score, feedback));
    }

    public Task<string> GenerateProfessionalIntroductionAsync(
        InterviewAiProvider provider,
        string candidateName,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        CancellationToken cancellationToken = default)
    {
        var difficultyLabel = difficulty.ToString().ToLowerInvariant();
        var categoryLabel = CategoryLabel(category);
        var name = string.IsNullOrWhiteSpace(candidateName) ? "candidate" : candidateName.Trim();
        return Task.FromResult($"Good day {name}. I am your AI interviewer for this {categoryLabel} interview for the {jobRole} role at {difficultyLabel} level. Take your time, and we will proceed one question at a time.");
    }

    public Task<string> GenerateEncouragementAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        string candidateTranscript,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidateTranscript))
        {
            return Task.FromResult("Take a moment and start with your key idea; I am listening.");
        }

        var wordCount = candidateTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < 18)
        {
            return Task.FromResult("Good start. Please add one concrete example or technical detail to strengthen your answer.");
        }

        return Task.FromResult("Thank you. That is a thoughtful answer. Let us move to a related follow-up.");
    }

    public Task<InterviewQuestion> GenerateFollowUpQuestionAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        InterviewQuestion previousQuestion,
        string candidateTranscript,
        IReadOnlyList<InterviewConversationTurn> conversationTurns,
        CancellationToken cancellationToken = default)
    {
        var followPrompt = $"{previousQuestion.Prompt} Based on your last answer, explain one specific tradeoff or real-world implementation detail.";
        var hint = $"Must reference: {previousQuestion.IdealAnswerHint}. Add one concrete example and limitation handling.";
        return Task.FromResult(new InterviewQuestion
        {
            Prompt = followPrompt,
            IdealAnswerHint = hint,
        });
    }

    public Task<double> CalculateRoleFitScoreAsync(
        InterviewAiProvider provider,
        string jobRole,
        string jobDescription,
        InterviewDifficulty difficulty,
        IReadOnlyList<InterviewQuestionResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
        {
            return Task.FromResult(0d);
        }

        var avg = results.Average(x => x.ScoreAwarded);
        var correctnessBoost = (double)results.Count(x => x.IsCorrect) / results.Count * 20;
        var raw = Math.Min(100, avg * 0.8 + correctnessBoost);
        var difficultyAdjust = difficulty switch
        {
            InterviewDifficulty.Professional => 1.05,
            InterviewDifficulty.Experienced => 1.0,
            _ => 0.95,
        };

        var finalScore = Math.Round(Math.Min(100, raw * difficultyAdjust), 2);
        return Task.FromResult(finalScore);
    }

    private static List<InterviewQuestion> BuildQuestionPool(string role, InterviewCategory category, string jobDescription, InterviewDifficulty difficulty)
    {
        var difficultySuffix = difficulty switch
        {
            InterviewDifficulty.Professional => "Include architecture, tradeoff analysis, and production-scale constraints.",
            InterviewDifficulty.Experienced => "Include practical implementation and debugging depth.",
            _ => "Keep fundamentals clear and hands-on for entry-level readiness.",
        };

        if (category == InterviewCategory.Behavioral)
        {
            return
            [
                NewQuestion("Tell me about a difficult team situation and how you handled it.", $"Conflict resolution, communication, ownership, outcome. {difficultySuffix}"),
                NewQuestion("Describe a time you received critical feedback. What did you change?", $"Self-awareness, action, measurable improvement. {difficultySuffix}"),
                NewQuestion("How do you prioritize when multiple urgent tasks arrive together?", $"Prioritization framework, stakeholder communication, risk handling. {difficultySuffix}"),
                NewQuestion("Share an example where you influenced a decision without authority.", $"Data-driven persuasion, empathy, collaboration, result. {difficultySuffix}"),
                NewQuestion("Describe a failure and what you learned from it.", $"Accountability, reflection, prevention strategy. {difficultySuffix}"),
            ];
        }

        if (category == InterviewCategory.Hr)
        {
            return
            [
                NewQuestion("Why do you want this role, and why now?", $"Motivation alignment, role clarity, growth intent. {difficultySuffix}"),
                NewQuestion("What work environment helps you perform at your best?", $"Self-awareness, collaboration style, adaptability. {difficultySuffix}"),
                NewQuestion("How do you handle stress during deadlines?", $"Coping mechanisms, planning, communication. {difficultySuffix}"),
                NewQuestion("What are your top strengths and one area you are improving?", $"Evidence-backed strengths, growth plan. {difficultySuffix}"),
                NewQuestion("How do you ensure professionalism in remote or hybrid setups?", $"Discipline, communication cadence, reliability. {difficultySuffix}"),
            ];
        }

        if (category == InterviewCategory.Management)
        {
            return
            [
                NewQuestion("How do you align team execution with business goals?", $"Strategy translation, planning, KPI tracking. {difficultySuffix}"),
                NewQuestion("Describe your approach to handling underperformance.", $"Coaching, expectations, accountability, follow-through. {difficultySuffix}"),
                NewQuestion("How do you make decisions under ambiguity?", $"Decision framework, risk management, stakeholder alignment. {difficultySuffix}"),
                NewQuestion("How do you structure delegation while maintaining quality?", $"Ownership model, review loops, escalation paths. {difficultySuffix}"),
                NewQuestion("How do you balance delivery speed with long-term technical health?", $"Tradeoffs, roadmap thinking, debt management. {difficultySuffix}"),
            ];
        }

        var roleLower = role.ToLowerInvariant();
        if (roleLower.Contains("backend") || roleLower.Contains("api"))
        {
            return
            [
                NewQuestion("Explain how you design a REST API for versioning and backward compatibility.", $"Version strategy, URI or header versioning, deprecation strategy, contract compatibility. {difficultySuffix}"),
                NewQuestion("How would you optimize a slow database query in production?", $"Use indexes, execution plans, caching, avoid N+1, monitor latency. {difficultySuffix}"),
                NewQuestion("What strategies do you use for secure authentication and authorization?", $"JWT/session, password hashing, RBAC, least privilege, token expiry. {difficultySuffix}"),
                NewQuestion("Describe how you would handle distributed system failures.", $"Retries, circuit breaker, idempotency, timeout, observability. {difficultySuffix}"),
                NewQuestion("How do you approach writing testable backend code?", $"Separation of concerns, dependency injection, unit/integration tests. {difficultySuffix}"),
            ];
        }

        if (roleLower.Contains("frontend") || roleLower.Contains("ui"))
        {
            return
            [
                NewQuestion("How do you improve frontend performance in a large app?", $"Code splitting, lazy loading, memoization, bundle analysis, caching. {difficultySuffix}"),
                NewQuestion("Explain component state management tradeoffs.", $"Local vs global state, predictability, complexity, maintainability. {difficultySuffix}"),
                NewQuestion("How do you ensure accessibility in your UI?", $"Semantic HTML, keyboard nav, ARIA, contrast, screen reader checks. {difficultySuffix}"),
                NewQuestion("How do you design responsive layouts across devices?", $"Mobile-first, breakpoints, flexible grid, media queries. {difficultySuffix}"),
                NewQuestion("How do you handle error states and loading states for users?", $"Skeletons, retries, clear messages, graceful fallbacks. {difficultySuffix}"),
            ];
        }

        return
        [
            NewQuestion("Tell us about a challenging project and how you solved it.", $"Problem breakdown, action, collaboration, measurable impact. {difficultySuffix}"),
            NewQuestion("How do you prioritize tasks when requirements change?", $"Communication, impact analysis, iteration planning. {difficultySuffix}"),
            NewQuestion("Explain your approach to debugging complex issues.", $"Reproduce issue, isolate variables, logs, tools, validate fix. {difficultySuffix}"),
            NewQuestion("How do you ensure quality before releasing a feature?", $"Testing, review, monitoring, rollback plan. {difficultySuffix}"),
            NewQuestion("Why are you suitable for this role based on the job description?", $"{jobDescription} {difficultySuffix}"),
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

    private static string CategoryLabel(InterviewCategory category)
    {
        return category switch
        {
            InterviewCategory.Behavioral => "behavioral",
            InterviewCategory.Hr => "HR",
            InterviewCategory.Management => "management",
            _ => "technical",
        };
    }
}
