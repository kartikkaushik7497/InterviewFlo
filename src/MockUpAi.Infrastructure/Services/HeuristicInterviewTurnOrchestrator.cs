using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Infrastructure.Services;

internal sealed class HeuristicInterviewTurnOrchestrator
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "answer", "before", "candidate", "cover", "describe", "does", "expected",
        "explain", "final", "give", "have", "hint", "interview", "medium", "mention", "please",
        "question", "role", "section", "short", "string", "tell", "that", "this", "what", "when",
        "where", "which", "while", "with", "would", "your", "you", "aim", "seconds", "minutes",
    };

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
        var signals = AnalyzeSignals(answer);

        var hintKeywords = Keywords(currentQuestion.IdealAnswerHint);
        var questionKeywords = Keywords(currentQuestion.Prompt);
        var answerKeywords = Keywords(answer);
        var jobKeywords = Keywords(session.JobDescription)
            .Where(keyword => keyword.Length > 3)
            .Take(18)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hintOverlap = PercentageOverlap(hintKeywords, answerKeywords);
        var promptOverlap = PercentageOverlap(questionKeywords, answerKeywords);
        var jobOverlap = PercentageOverlap(jobKeywords, answerKeywords);
        var section = ResolveSection(currentQuestion.IdealAnswerHint);
        var expectedWords = ResolveExpectedWordCount(currentQuestion.IdealAnswerHint, section);
        var isLowQuality = IsLowQuality(answer);

        var relevance = CalculateRelevance(section, hintOverlap, jobOverlap, signals);
        var depth = CalculateDepth(section, wordCount, signals);
        var communication = Math.Clamp(38d + Math.Min(wordCount, 65) * 0.7 + signals.StructureBonus - signals.NoisePenalty, 0d, 100d);
        var technical = CalculateTechnical(section, hintOverlap, jobOverlap, signals);
        var problemSolving = CalculateProblemSolving(section, wordCount, signals);
        var rawScore = WeightedForSection(section, technical, depth, relevance, communication, problemSolving);
        var overall = ApplyVerificationCaps(
            rawScore,
            section,
            signals,
            wordCount,
            expectedWords,
            hintOverlap,
            promptOverlap,
            jobOverlap,
            isLowQuality);

        var needsClarification = currentQuestionIndex < totalQuestionCount - 1 &&
                                 (isLowQuality ||
                                  wordCount < expectedWords ||
                                  NeedsSectionClarification(section, signals, promptOverlap, hintOverlap, jobOverlap));

        var nextTopic = NextTopic(session, currentQuestionIndex);
        var nextQuestion = needsClarification
            ? BuildClarificationPrompt(session.JobRole, section, signals, expectedWords)
            : BuildScenarioQuestion(session.JobRole, session.Difficulty, nextTopic);

        var strengths = BuildStrengths(signals, hintOverlap, jobOverlap, overall);
        var gaps = BuildGaps(section, signals, wordCount, expectedWords, hintOverlap, promptOverlap, jobOverlap);

        return Task.FromResult(new InterviewTurnDecision
        {
            Acknowledgement = BuildAcknowledgement(overall, needsClarification, strengths, gaps),
            ClarificationPrompt = needsClarification ? nextQuestion : null,
            NextQuestion = nextQuestion,
            IdealAnswerHint = needsClarification
                ? "Use a specific situation, your exact action, a technical decision or tradeoff, and a measurable result."
                : $"Strong answer should cover {nextTopic}, practical decisions, failure cases, tradeoffs, and business/user impact.",
            Topic = needsClarification ? currentQuestion.Prompt : nextTopic,
            ShouldAskClarification = needsClarification,
            ShouldMoveToNewTopic = !needsClarification,
            TechnicalScore = Math.Round(technical, 2),
            CommunicationScore = Math.Round(communication, 2),
            DepthScore = Math.Round(depth, 2),
            RelevanceScore = Math.Round(relevance, 2),
            ProblemSolvingScore = Math.Round(problemSolving, 2),
            OverallScore = Math.Round(overall, 2),
            EvaluationSummary = BuildEvaluationSummary(overall, strengths, gaps),
            ObservedStrengths = strengths,
            ObservedGaps = gaps,
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

    private static double WeightedForSection(string section, double technical, double depth, double relevance, double communication, double problemSolving)
    {
        return GetRubricWeights(section).Score(technical, depth, relevance, communication, problemSolving);
    }

    private static RubricWeights GetRubricWeights(string section)
    {
        if (IsSection(section, "career", "education", "hr"))
        {
            return new RubricWeights(
                Technical: 0.10,
                Depth: 0.20,
                Relevance: 0.30,
                Communication: 0.30,
                ProblemSolving: 0.10);
        }

        if (IsSection(section, "project", "behavioral", "management"))
        {
            return new RubricWeights(
                Technical: 0.10,
                Depth: 0.28,
                Relevance: 0.22,
                Communication: 0.18,
                ProblemSolving: 0.22);
        }

        if (IsSection(section, "coding", "oop", "api", "database", "security", "testing", "frontend", "technical", "reliability", "debugging"))
        {
            return new RubricWeights(
                Technical: 0.35,
                Depth: 0.25,
                Relevance: 0.15,
                Communication: 0.15,
                ProblemSolving: 0.10);
        }

        return new RubricWeights(
            Technical: 0.30,
            Depth: 0.24,
            Relevance: 0.20,
            Communication: 0.16,
            ProblemSolving: 0.10);
    }

    private static double CalculateRelevance(string section, double hintOverlap, double jobOverlap, AnswerSignals signals)
    {
        var baseScore = hintOverlap * 0.50 + jobOverlap * 0.25 + signals.RoleSignalBonus;
        if (IsSection(section, "career", "education", "hr"))
        {
            baseScore += signals.HasContext ? 16 : 6;
            baseScore += signals.HasCandidateAction ? 8 : 0;
        }

        if (IsSection(section, "project", "behavioral", "management"))
        {
            baseScore += signals.HasContext ? 14 : 0;
            baseScore += signals.HasConcreteOutcome ? 10 : 0;
        }

        return Math.Clamp(baseScore, 0d, 100d);
    }

    private static double CalculateDepth(string section, int wordCount, AnswerSignals signals)
    {
        var wordFactor = IsSection(section, "career", "education", "hr") ? 1.05 : 0.9;
        var score = wordCount * wordFactor + signals.DetailBonus + signals.TradeoffBonus + signals.OutcomeBonus;
        if (IsSection(section, "project", "behavioral", "management"))
        {
            score += signals.ContextBonus + signals.ActionBonus;
        }

        return Math.Clamp(score, 0d, 100d);
    }

    private static double CalculateTechnical(string section, double hintOverlap, double jobOverlap, AnswerSignals signals)
    {
        if (IsSection(section, "career", "education", "hr", "behavioral", "management"))
        {
            return Math.Clamp(30d + jobOverlap * 0.25 + hintOverlap * 0.25 + (signals.HasTechnicalDetail ? 18 : 0), 0d, 100d);
        }

        return Math.Clamp(hintOverlap * 0.45 + jobOverlap * 0.25 + signals.TechnicalBonus + signals.TradeoffBonus, 0d, 100d);
    }

    private static double CalculateProblemSolving(string section, int wordCount, AnswerSignals signals)
    {
        var score = signals.ActionBonus + signals.ContextBonus + signals.TradeoffBonus + signals.OutcomeBonus + Math.Min(wordCount, 70) * 0.5;
        if (IsSection(section, "coding", "oop", "api", "database", "security", "testing", "frontend", "technical", "reliability", "debugging"))
        {
            score += signals.HasTechnicalDetail ? 14 : 0;
        }

        return Math.Clamp(score, 0d, 100d);
    }

    private static double ApplyVerificationCaps(
        double score,
        string section,
        AnswerSignals signals,
        int wordCount,
        int expectedWords,
        double hintOverlap,
        double promptOverlap,
        double jobOverlap,
        bool isLowQuality)
    {
        if (isLowQuality || wordCount == 0)
        {
            return 0;
        }

        var cap = 100d;
        var minimumUsefulWords = Math.Max(4, expectedWords / 2);
        if (wordCount < minimumUsefulWords)
        {
            cap = Math.Min(cap, 32);
        }
        else if (wordCount < expectedWords)
        {
            cap = Math.Min(cap, 58);
        }

        var hasAnyRelevance = promptOverlap >= 8 || hintOverlap >= 8 || jobOverlap >= 8;
        if (!hasAnyRelevance && !signals.HasContext && !signals.HasTechnicalDetail)
        {
            cap = Math.Min(cap, 34);
        }

        if (IsSection(section, "career", "education", "hr"))
        {
            if (!signals.HasContext && promptOverlap < 10 && jobOverlap < 10)
            {
                cap = Math.Min(cap, 46);
            }

            if (!signals.HasStructure && wordCount < expectedWords + 6)
            {
                cap = Math.Min(cap, 68);
            }
        }
        else if (IsSection(section, "project", "behavioral", "management"))
        {
            if (!signals.HasContext)
            {
                cap = Math.Min(cap, 44);
            }

            if (!signals.HasCandidateAction)
            {
                cap = Math.Min(cap, 50);
            }

            if (!signals.HasConcreteOutcome)
            {
                cap = Math.Min(cap, 70);
            }

            if (IsSection(section, "project") && !signals.HasTechnicalDetail)
            {
                cap = Math.Min(cap, 64);
            }
        }
        else if (IsSection(section, "coding", "oop", "api", "database", "security", "testing", "frontend", "technical", "reliability", "debugging"))
        {
            if (!signals.HasTechnicalDetail)
            {
                cap = Math.Min(cap, 42);
            }

            if (promptOverlap < 8 && hintOverlap < 8)
            {
                cap = Math.Min(cap, 52);
            }
        }

        return Math.Round(Math.Min(score, cap), 2);
    }

    private static bool NeedsSectionClarification(
        string section,
        AnswerSignals signals,
        double promptOverlap,
        double hintOverlap,
        double jobOverlap)
    {
        if (IsSection(section, "career", "education", "hr"))
        {
            return !signals.HasContext && promptOverlap < 10 && jobOverlap < 10;
        }

        if (IsSection(section, "project"))
        {
            return !signals.HasCandidateAction || !signals.HasConcreteOutcome;
        }

        if (IsSection(section, "behavioral", "management"))
        {
            return !signals.HasCandidateAction;
        }

        if (IsSection(section, "coding", "oop", "api", "database", "security", "testing", "frontend", "technical", "reliability", "debugging"))
        {
            return !signals.HasTechnicalDetail || (promptOverlap < 8 && hintOverlap < 8);
        }

        return false;
    }

    private static string ResolveSection(string hint)
    {
        const string marker = "Section:";
        var index = hint.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return "technical";
        }

        var start = index + marker.Length;
        var end = hint.IndexOf(';', start);
        var value = end >= 0 ? hint[start..end] : hint[start..];
        return string.IsNullOrWhiteSpace(value) ? "technical" : value.Trim().ToLowerInvariant();
    }

    private static int ResolveExpectedWordCount(string hint, string section)
    {
        const string marker = "Expected:";
        var index = hint.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var start = index + marker.Length;
            var end = hint.IndexOf(';', start);
            var value = (end >= 0 ? hint[start..end] : hint[start..]).Trim();
            return value.ToLowerInvariant() switch
            {
                "long" => 34,
                "medium" => 18,
                "short" => 10,
                _ => 14,
            };
        }

        if (IsSection(section, "career", "education", "hr"))
        {
            return 14;
        }

        if (IsSection(section, "project", "behavioral", "management"))
        {
            return 24;
        }

        return 10;
    }

    private static bool IsSection(string section, params string[] values)
    {
        return values.Any(value => section.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static AnswerSignals AnalyzeSignals(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return new AnswerSignals();
        }

        var normalized = answer.ToLowerInvariant();
        var hasContext = ContainsAny(normalized, "project", "system", "feature", "module", "team", "client", "production", "requirement", "problem", "issue");
        var hasCandidateAction = ContainsAny(normalized, "i built", "i created", "i designed", "i implemented", "i developed", "i worked", "i fixed", "i debugged", "i optimized", "i added", "i used", "my role", "my task", "currently working", "we built", "we implemented", "we developed");
        var hasTechnicalDetail = ContainsAny(
            normalized,
            "api", "database", "query", "index", "cache", "authentication", "authorization", "jwt", "component", "state",
            "service", "repository", "endpoint", "deployment", "logging", "monitoring", "node", "react", ".net", "mongo",
            "sql", "azure", "docker", "kubernetes", "pipeline", "ci/cd", "cloud", "load balancer", "rollback", "health check",
            "array", "list", "hash map", "hashmap", "dictionary", "loop", "class", "interface", "object", "inheritance",
            "composition", "polymorphism", "encapsulation", "validation", "input", "output", "complexity", "edge case",
            "test case", "regression", "smoke", "sanity", "automation", "selenium", "postman", "bug report", "severity",
            "priority", "sql", "join", "group by", "dashboard", "kpi", "metric", "outlier", "missing values", "visualization",
            "python", "pandas", "model", "training", "validation", "precision", "recall", "f1", "overfitting", "drift",
            "owasp", "xss", "injection", "mfa", "encryption", "least privilege", "incident", "vulnerability", "backup",
            "restore", "transaction", "constraint", "replication", "sqlite", "secure storage", "offline", "latency");
        var hasTradeoff = ContainsAny(normalized, "tradeoff", "trade-off", "because", "however", "instead", "risk", "latency", "performance", "security", "scalability", "maintainability", "cost");
        var hasOutcome = ContainsAny(normalized, "result", "impact", "improved", "reduced", "increased", "faster", "slower", "users", "production", "released", "resolved", "fixed") || answer.Any(char.IsDigit);
        var hasStructure = ContainsAny(normalized, "first", "then", "after that", "finally", "because", "so", "therefore", "for example");

        return new AnswerSignals(
            HasContext: hasContext,
            HasCandidateAction: hasCandidateAction,
            HasTechnicalDetail: hasTechnicalDetail,
            HasTradeoff: hasTradeoff,
            HasConcreteOutcome: hasOutcome,
            HasStructure: hasStructure,
            ContextBonus: hasContext ? 14 : 0,
            ActionBonus: hasCandidateAction ? 22 : 4,
            TechnicalBonus: hasTechnicalDetail ? 30 : 6,
            TradeoffBonus: hasTradeoff ? 18 : 0,
            OutcomeBonus: hasOutcome ? 16 : 0,
            StructureBonus: hasStructure ? 14 : 0,
            DetailBonus: (hasContext ? 10 : 0) + (hasTechnicalDetail ? 18 : 0),
            RoleSignalBonus: hasTechnicalDetail ? 20 : 4,
            NoisePenalty: IsLowQuality(answer) ? 40 : 0);
    }

    private static IReadOnlyList<string> BuildStrengths(AnswerSignals signals, double hintOverlap, double jobOverlap, double overall)
    {
        var strengths = new List<string>();
        if (signals.HasCandidateAction)
        {
            strengths.Add("clear ownership of the work");
        }

        if (signals.HasTechnicalDetail)
        {
            strengths.Add("practical technical detail");
        }

        if (signals.HasTradeoff)
        {
            strengths.Add("tradeoff awareness");
        }

        if (signals.HasConcreteOutcome)
        {
            strengths.Add("result or impact signal");
        }

        if (hintOverlap >= 35 || jobOverlap >= 25)
        {
            strengths.Add("role-aligned evidence");
        }

        if (strengths.Count == 0 && overall >= 45)
        {
            strengths.Add("some relevant answer signal");
        }

        return strengths;
    }

    private static IReadOnlyList<string> BuildGaps(
        string section,
        AnswerSignals signals,
        int wordCount,
        int expectedWords,
        double hintOverlap,
        double promptOverlap,
        double jobOverlap)
    {
        var gaps = new List<string>();
        if (wordCount < expectedWords)
        {
            gaps.Add(IsSection(section, "project")
                ? "project answer needs more detail"
                : "answer is too brief");
        }

        if (IsSection(section, "career", "education", "hr"))
        {
            if (promptOverlap < 10 && hintOverlap < 15 && jobOverlap < 15)
            {
                gaps.Add("connect background to the job post");
            }

            return gaps.Count == 0 ? ["add one specific achievement"] : gaps;
        }

        if (IsSection(section, "project", "behavioral", "management"))
        {
            if (!signals.HasCandidateAction)
            {
                gaps.Add("needs your exact action");
            }

            if (!signals.HasConcreteOutcome)
            {
                gaps.Add("add result or measurable impact");
            }

            if (IsSection(section, "project") && !signals.HasTechnicalDetail)
            {
                gaps.Add("add stack or implementation detail");
            }

            return gaps.Count == 0 ? ["add one tradeoff or decision reason"] : gaps;
        }

        if (!signals.HasTechnicalDetail)
        {
            gaps.Add("needs implementation detail");
        }

        if (!signals.HasTradeoff && !IsSection(section, "coding"))
        {
            gaps.Add("add one tradeoff or decision reason");
        }

        if (promptOverlap < 8 && hintOverlap < 20 && jobOverlap < 15)
        {
            gaps.Add("answer does not match the question closely enough");
        }

        return gaps.Count == 0 ? ["probe for production constraints"] : gaps;
    }

    private static string BuildAcknowledgement(double score, bool needsClarification, IReadOnlyList<string> strengths, IReadOnlyList<string> gaps)
    {
        if (needsClarification)
        {
            var gap = gaps.FirstOrDefault() ?? "more concrete evidence";
            return $"Thanks. I caught the direction, but I need {gap} before I can score this fairly.";
        }

        if (score >= 74)
        {
            var strength = strengths.FirstOrDefault() ?? "good role signal";
            return $"Good, that gives me {strength}. I am going to increase the depth now.";
        }

        return "Thanks. There is useful signal there; I am going to test the same skill in a more practical scenario.";
    }

    private static string BuildEvaluationSummary(double score, IReadOnlyList<string> strengths, IReadOnlyList<string> gaps)
    {
        var strengthText = strengths.Count == 0 ? "limited strengths captured" : string.Join(", ", strengths.Take(2));
        var gapText = gaps.Count == 0 ? "no major gaps" : string.Join(", ", gaps.Take(2));
        return $"Score signal: {score:0.#}/100. Strengths: {strengthText}. Improve: {gapText}.";
    }

    private static string BuildClarificationPrompt(string role, string section, AnswerSignals signals, int expectedWords)
    {
        if (IsSection(section, "career", "education", "hr"))
        {
            return $"Add a little more context for the {role} role: education or experience, one relevant skill, and why it fits this job.";
        }

        if (IsSection(section, "coding"))
        {
            return "Give the approach in steps, mention the data structure, and add one edge case.";
        }

        if (IsSection(section, "oop"))
        {
            return "Give one small class or interface example and explain why that design is better.";
        }

        if (!signals.HasCandidateAction)
        {
            return $"Let us make this concrete. For one real {role} project, what exactly did you personally build, debug, or decide?";
        }

        if (!signals.HasTechnicalDetail)
        {
            return "Add the technical layer: what tools, API, database, component, or service design did you use, and why?";
        }

        if (!signals.HasTradeoff)
        {
            return "In that project, what technical choice did you make and why was it the right approach?";
        }

        return "What result did your work create? Mention one outcome, metric, user impact, or lesson learned.";
    }

    private static string BuildScenarioQuestion(string role, InterviewDifficulty difficulty, string topic)
    {
        var roleLabel = string.IsNullOrWhiteSpace(role) ? "this role" : role.Trim();
        var productionDepth = difficulty == InterviewDifficulty.Fresher
            ? "keep it practical and explain the steps you would take"
            : "include the tradeoffs, failure cases, monitoring, and rollback plan";

        return topic switch
        {
            "frontend-backend integration" => $"Imagine the UI is showing stale data after an API update. As a {roleLabel}, how would you trace the issue from browser to backend and fix it? Please {productionDepth}.",
            "API reliability" => $"A production API starts timing out for some users. Walk me through how you would diagnose it, stabilize it, and prevent it from returning. Please {productionDepth}.",
            "database performance" => $"A page is slow because the data query takes too long. How would you investigate the database, improve performance, and verify the fix? Please {productionDepth}.",
            "authentication and security" => $"You are asked to secure login and protected routes for this product. What design would you use for authentication, authorization, token/session handling, and abuse prevention? Please {productionDepth}.",
            "testing and release confidence" => $"Before releasing a risky feature, what tests, checks, and observability would you put in place so the team can ship confidently? Please {productionDepth}.",
            "production debugging" => $"A feature worked locally but fails in production. How would you debug it without guessing, and what signals would you collect first? Please {productionDepth}.",
            "component state and UX" => $"A user flow has loading, empty, error, and success states. How would you design the component state and UX so it feels reliable? Please {productionDepth}.",
            "accessibility and responsiveness" => $"How would you make an interview screen accessible, keyboard-friendly, and responsive without making the interface feel cluttered? Please {productionDepth}.",
            "mobile reliability" => $"A mobile app works on Wi-Fi but fails often on weak networks. How would you design request handling, local state, and user feedback? Please {productionDepth}.",
            "mobile performance" => $"A mobile screen scrolls poorly and images load slowly. How would you diagnose and improve the experience? Please {productionDepth}.",
            "test strategy" => $"A new candidate creation feature is ready for QA. How would you design the test strategy, test data, and release checks? Please {productionDepth}.",
            "bug triage" => $"A high-priority bug is reported just before release. How would you verify, prioritize, communicate, and help decide whether to ship? Please {productionDepth}.",
            "ci/cd pipeline" => $"Design a safe CI/CD pipeline for this desktop interview app. What stages, checks, secrets, and rollback controls would you include? Please {productionDepth}.",
            "cloud incident response" => $"The app suddenly cannot reach its cloud database. How would you investigate, communicate status, and reduce future downtime? Please {productionDepth}.",
            "data quality" => $"A dashboard metric looks wrong during a leadership review. How would you validate the data source, transformation, and final number? Please {productionDepth}.",
            "business insight" => $"You are asked to analyze candidate interview performance trends. What metrics would you define, and how would you present useful insights? Please {productionDepth}.",
            "model evaluation" => $"An ML model performs well in testing but poorly for real users. How would you evaluate, debug, and monitor it? Please {productionDepth}.",
            "ml deployment" => $"How would you deploy and monitor an ML model so the team can detect drift, latency issues, and bad predictions? Please {productionDepth}.",
            "application security" => $"You are asked to review this app before production. What authentication, authorization, secrets, and logging risks would you check first? Please {productionDepth}.",
            "incident investigation" => $"Suspicious login attempts appear in logs. How would you investigate, contain risk, and improve prevention? Please {productionDepth}.",
            "database operations" => $"A dashboard query is slow and the database load is high. How would you diagnose indexes, query shape, and production risk? Please {productionDepth}.",
            "backup and recovery" => $"How would you design backup, restore testing, and recovery objectives for candidate/interview data? Please {productionDepth}.",
            _ => $"Let us move into a realistic scenario for the {roleLabel} role. Tell me how you would approach {topic}, what risks you would watch for, and how you would prove your solution worked. Please {productionDepth}.",
        };
    }

    private static string NextTopic(InterviewSession session, int index)
    {
        var role = session.JobRole.ToLowerInvariant();
        string[] topics;

        if (role.Contains("full stack") || role.Contains("fullstack"))
        {
            topics = ["frontend-backend integration", "API reliability", "database performance", "authentication and security", "testing and release confidence"];
        }
        else if (role.Contains("backend") || role.Contains("api") || role.Contains("node") || role.Contains(".net"))
        {
            topics = ["API reliability", "database performance", "authentication and security", "testing and release confidence", "production debugging"];
        }
        else if (role.Contains("frontend") || role.Contains("ui") || role.Contains("react"))
        {
            topics = ["component state and UX", "frontend-backend integration", "accessibility and responsiveness", "testing and release confidence", "production debugging"];
        }
        else if (role.Contains("mobile") || role.Contains("android") || role.Contains("ios") || role.Contains("flutter") || role.Contains("react native"))
        {
            topics = ["mobile reliability", "mobile performance", "frontend-backend integration", "testing and release confidence", "application security"];
        }
        else if (role.Contains("qa") || role.Contains("quality") || role.Contains("test"))
        {
            topics = ["test strategy", "bug triage", "API reliability", "testing and release confidence", "production debugging"];
        }
        else if (role.Contains("devops") || role.Contains("cloud") || role.Contains("sre"))
        {
            topics = ["ci/cd pipeline", "cloud incident response", "production debugging", "application security", "testing and release confidence"];
        }
        else if (role.Contains("data analyst") || role.Contains("analytics") || role.Contains("bi "))
        {
            topics = ["data quality", "business insight", "database performance", "testing and release confidence", "production debugging"];
        }
        else if (role.Contains("machine learning") || role.Contains("ml ") || role.Contains("ai engineer"))
        {
            topics = ["model evaluation", "ml deployment", "data quality", "production debugging", "testing and release confidence"];
        }
        else if (role.Contains("cyber") || role.Contains("security"))
        {
            topics = ["application security", "incident investigation", "authentication and security", "production debugging", "testing and release confidence"];
        }
        else if (role.Contains("database") || role.Contains("dba"))
        {
            topics = ["database operations", "backup and recovery", "database performance", "application security", "production debugging"];
        }
        else
        {
            topics = ["project execution", "problem solving", "technical fundamentals", "collaboration", "quality and delivery"];
        }

        return topics[Math.Clamp(index + 1, 0, topics.Length - 1)];
    }

    private static double PercentageOverlap(IReadOnlySet<string> expected, IReadOnlySet<string> actual)
    {
        if (expected.Count == 0)
        {
            return 0;
        }

        return expected.Count(actual.Contains) * 100.0 / expected.Count;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
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
            .Split([' ', ',', '.', ';', ':', '-', '_', '\n', '\r', '\t', '(', ')', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 2 && !StopWords.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record AnswerSignals(
        bool HasContext = false,
        bool HasCandidateAction = false,
        bool HasTechnicalDetail = false,
        bool HasTradeoff = false,
        bool HasConcreteOutcome = false,
        bool HasStructure = false,
        double ContextBonus = 0,
        double ActionBonus = 0,
        double TechnicalBonus = 0,
        double TradeoffBonus = 0,
        double OutcomeBonus = 0,
        double StructureBonus = 0,
        double DetailBonus = 0,
        double RoleSignalBonus = 0,
        double NoisePenalty = 0);

    private sealed record RubricWeights(
        double Technical,
        double Depth,
        double Relevance,
        double Communication,
        double ProblemSolving)
    {
        public double Score(double technical, double depth, double relevance, double communication, double problemSolving)
        {
            return Math.Clamp(
                technical * Technical +
                depth * Depth +
                relevance * Relevance +
                communication * Communication +
                problemSolving * ProblemSolving,
                0,
                100);
        }
    }
}
