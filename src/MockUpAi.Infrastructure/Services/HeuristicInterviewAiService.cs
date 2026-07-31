using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Infrastructure.Services;

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
        var chosen = BuildQuestionPlan(pool, category, count);

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
        var categoryLabel = CategoryLabel(category);
        var name = string.IsNullOrWhiteSpace(candidateName) ? "candidate" : candidateName.Trim();
        return Task.FromResult($"Hi {name}, I will ask focused {categoryLabel} questions for the {jobRole} role; answer naturally with concrete examples.");
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
        var roleLower = role.ToLowerInvariant();
        var jobFocus = BuildJobFocus(jobDescription);

        if (category == InterviewCategory.Behavioral)
        {
            return
            [
                NewQuestion("Intro: summarize your background for this role. Aim for 60-90 seconds.", "career", "medium", $"Relevant background, education, role motivation, communication clarity. {difficultySuffix}"),
                NewQuestion("Project: describe one experience that shows ownership. Aim for 2 minutes.", "project", "long", $"Situation, action, collaboration, result, learning. {difficultySuffix}"),
                NewQuestion("Tell me about a difficult team situation and how you handled it.", "behavioral", "medium", $"Conflict resolution, communication, ownership, outcome. {difficultySuffix}"),
                NewQuestion("Describe a time you received critical feedback. What did you change?", "behavioral", "medium", $"Self-awareness, action, measurable improvement. {difficultySuffix}"),
                NewQuestion("How do you prioritize when multiple urgent tasks arrive together?", "behavioral", "short", $"Prioritization framework, stakeholder communication, risk handling. {difficultySuffix}"),
                NewQuestion("Share an example where you influenced a decision without authority.", "behavioral", "medium", $"Data-driven persuasion, empathy, collaboration, result. {difficultySuffix}"),
                NewQuestion("Describe a failure and what you learned from it.", "behavioral", "medium", $"Accountability, reflection, prevention strategy. {difficultySuffix}"),
                NewQuestion("How do you communicate risk before a deadline slips?", "behavioral", "short", $"Early escalation, options, expectation management, ownership. {difficultySuffix}"),
                NewQuestion("What helps you work well with engineers, managers, or clients?", "behavioral", "short", $"Stakeholder empathy, clarity, follow-up, accountability. {difficultySuffix}"),
                NewQuestion("Tell me about a time you learned something fast for work.", "behavioral", "medium", $"Learning plan, application, result. {difficultySuffix}"),
                NewQuestion("How do you handle disagreement during a review?", "behavioral", "short", $"Respectful reasoning, evidence, compromise. {difficultySuffix}"),
                NewQuestion("Tell me about a time requirements changed late. What did you do?", "behavioral", "medium", $"Adaptability, communication, reprioritization, result. {difficultySuffix}"),
                NewQuestion("Describe a time you improved a process or workflow.", "behavioral", "medium", $"Problem, initiative, collaboration, measurable improvement. {difficultySuffix}"),
                NewQuestion("How do you stay calm and useful when there is ambiguity?", "behavioral", "short", $"Clarifying questions, assumptions, risk handling, ownership. {difficultySuffix}"),
                NewQuestion("Final: why are you a strong fit for this post? Aim for 60 seconds.", "career", "medium", $"Role alignment, confidence, evidence, growth intent. Job focus: {jobFocus}."),
            ];
        }

        if (category == InterviewCategory.Hr)
        {
            return
            [
                NewQuestion("Intro: summarize your education and work background. Aim for 60-90 seconds.", "career", "medium", $"Education, experience, role relevance, confidence. {difficultySuffix}"),
                NewQuestion("Why do you want this role, and why now?", "career", "medium", $"Motivation alignment, role clarity, growth intent. {difficultySuffix}"),
                NewQuestion("Which job requirement matches your strongest experience?", "career", "short", $"Direct job fit, evidence, role alignment. Job focus: {jobFocus}."),
                NewQuestion("What work environment helps you perform at your best?", "hr", "short", $"Self-awareness, collaboration style, adaptability. {difficultySuffix}"),
                NewQuestion("How do you handle stress during deadlines?", "hr", "short", $"Coping mechanisms, planning, communication. {difficultySuffix}"),
                NewQuestion("What are your top strengths and one area you are improving?", "hr", "medium", $"Evidence-backed strengths, growth plan. {difficultySuffix}"),
                NewQuestion("Tell me about one project or activity you are proud of.", "project", "long", $"Context, action, outcome, learning. {difficultySuffix}"),
                NewQuestion("How do you keep yourself accountable without close supervision?", "hr", "short", $"Ownership, planning, status updates, reliability. {difficultySuffix}"),
                NewQuestion("How do you learn a new tool or process quickly?", "hr", "short", $"Learning method, practice, result. {difficultySuffix}"),
                NewQuestion("How do you ensure professionalism in remote or hybrid setups?", "hr", "short", $"Discipline, communication cadence, reliability. {difficultySuffix}"),
                NewQuestion("What salary or growth expectations matter most to you?", "hr", "short", $"Realistic expectations, growth intent, communication. {difficultySuffix}"),
                NewQuestion("What motivates you to do your best work?", "hr", "short", $"Self-awareness, role fit, sustainable motivation. {difficultySuffix}"),
                NewQuestion("How do you respond when a manager gives direct feedback?", "hr", "short", $"Openness, action plan, measurable improvement. {difficultySuffix}"),
                NewQuestion("Where do you want to grow in the next one to two years?", "hr", "short", $"Growth direction, role alignment, practical goals. {difficultySuffix}"),
                NewQuestion("Final: why should the team select you for this post?", "career", "medium", $"Role fit, confidence, evidence, closing summary. Job focus: {jobFocus}."),
            ];
        }

        if (category == InterviewCategory.Management)
        {
            return
            [
                NewQuestion("Intro: summarize your leadership background. Aim for 60-90 seconds.", "career", "medium", $"Leadership scope, team size, delivery outcomes. {difficultySuffix}"),
                NewQuestion("Project: describe one high-impact delivery you led. Aim for 2 minutes.", "project", "long", $"Goal, team plan, risks, decisions, result. {difficultySuffix}"),
                NewQuestion("How do you align team execution with business goals?", "management", "short", $"Strategy translation, planning, KPI tracking. {difficultySuffix}"),
                NewQuestion("Describe your approach to handling underperformance.", "management", "medium", $"Coaching, expectations, accountability, follow-through. {difficultySuffix}"),
                NewQuestion("How do you make decisions under ambiguity?", "management", "short", $"Decision framework, risk management, stakeholder alignment. {difficultySuffix}"),
                NewQuestion("How do you structure delegation while maintaining quality?", "management", "short", $"Ownership model, review loops, escalation paths. {difficultySuffix}"),
                NewQuestion("How do you balance delivery speed with long-term technical health?", "management", "short", $"Tradeoffs, roadmap thinking, debt management. {difficultySuffix}"),
                NewQuestion("How do you communicate status to senior stakeholders?", "management", "short", $"Clarity, metrics, risks, decisions needed. {difficultySuffix}"),
                NewQuestion("What metrics tell you a team is healthy?", "management", "short", $"Delivery, quality, morale, predictability. {difficultySuffix}"),
                NewQuestion("How do you manage conflict between product and engineering?", "management", "medium", $"Tradeoff framing, evidence, alignment, outcome. {difficultySuffix}"),
                NewQuestion("How would you improve this job function in your first 90 days?", "management", "medium", $"Discovery, priorities, execution plan, measurement. Job focus: {jobFocus}."),
                NewQuestion("How do you plan work when scope is larger than team capacity?", "management", "medium", $"Prioritization, sequencing, stakeholder alignment, tradeoffs. {difficultySuffix}"),
                NewQuestion("How do you mentor someone who is technically strong but inconsistent?", "management", "medium", $"Coaching, expectations, feedback, accountability. {difficultySuffix}"),
                NewQuestion("How do you protect quality when leadership asks for faster delivery?", "management", "medium", $"Risk framing, options, quality gates, communication. {difficultySuffix}"),
                NewQuestion("Final: why are you the right fit for this post?", "career", "medium", $"Leadership fit, evidence, confidence, role alignment."),
            ];
        }

        if (roleLower.Contains("full stack") || roleLower.Contains("fullstack"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your experience for the {role} role. Aim for 60-90 seconds.", "career", "medium", $"Education, experience, frontend/backend stack familiarity, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one full-stack project you built. Cover the problem, frontend, backend, database, and result.", "project", "long", $"Project context, personal ownership, frontend, backend, database, result. {difficultySuffix}"),
                NewQuestion("Coding: how would you find duplicate values in an array or list?", "coding", "short", $"Hash set/map approach, complexity, edge cases. {difficultySuffix}"),
                NewQuestion("Coding: how would you check if a string has balanced brackets?", "coding", "short", $"Stack approach, matching pairs, invalid input, complexity. {difficultySuffix}"),
                NewQuestion("OOP: explain the four main OOP principles with a simple example.", "oop", "short", $"Encapsulation, abstraction, inheritance, polymorphism, example. {difficultySuffix}"),
                NewQuestion("API: what happens from the moment a user clicks a button until data is saved in the database?", "api", "medium", $"UI event, validation, API request, controller/service/repository, database, response, error handling. {difficultySuffix}"),
                NewQuestion("API: how should an app handle validation errors from the backend?", "api", "short", $"Status codes, field errors, user-friendly messages, retry/disable states. {difficultySuffix}"),
                NewQuestion("Frontend: how do you handle loading, empty, error, and success states?", "frontend", "short", $"State model, clear UX, retry, resilience. {difficultySuffix}"),
                NewQuestion("Database: what is an index, and when would you add one?", "database", "short", $"Index purpose, query performance, tradeoff, measurement. {difficultySuffix}"),
                NewQuestion("Database: when would you choose SQL over NoSQL for a feature?", "database", "short", $"Data relationships, transactions, schema, flexibility, use case. {difficultySuffix}"),
                NewQuestion("Security: how would you protect login and user data in a web app?", "security", "short", $"Password hashing, validation, authorization, sessions/tokens, secrets, least privilege. {difficultySuffix}"),
                NewQuestion("Debugging: a feature works locally but fails after deployment. What do you check first?", "debugging", "short", $"Logs, config, environment variables, API/database access, reproduction, rollback. {difficultySuffix}"),
                NewQuestion("Testing: what tests would you write before releasing a full-stack feature?", "testing", "short", $"Unit, integration, UI flow, auth, persistence, failure paths. {difficultySuffix}"),
                NewQuestion("Performance: how would you make a slow full-stack page faster?", "technical", "short", $"Measure first, frontend rendering, API latency, database query, caching. {difficultySuffix}"),
                NewQuestion("Behavioral: tell me about a challenging bug or deadline and how you handled it.", "behavioral", "medium", $"Situation, action, communication, technical decision, outcome. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this role? Aim for 60 seconds.", "career", "medium", $"Role fit, confidence, strongest evidence, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("backend") || roleLower.Contains("api") || roleLower.Contains("node") || roleLower.Contains(".net"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your backend development experience. Aim for 60-90 seconds.", "career", "medium", $"Education, backend experience, API/database familiarity, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one backend project you built. Cover the problem, API, database, and result.", "project", "long", $"Project context, ownership, API/database design, result. {difficultySuffix}"),
                NewQuestion("Coding: how would you count the frequency of words in a string?", "coding", "short", $"Hash map, parsing, complexity, edge cases. {difficultySuffix}"),
                NewQuestion("Coding: how would you find the first non-repeating character in a string?", "coding", "short", $"Frequency map, second pass, complexity, edge cases. {difficultySuffix}"),
                NewQuestion("OOP: explain dependency injection with one backend example.", "oop", "short", $"DI, testability, loose coupling, service boundaries. {difficultySuffix}"),
                NewQuestion("API: what makes a REST API well-designed?", "api", "short", $"Resource naming, methods, status codes, validation, errors, versioning, security. {difficultySuffix}"),
                NewQuestion("API: how do you design pagination, filtering, and sorting for a list endpoint?", "api", "short", $"Query parameters, limits, indexes, metadata, consistent response shape. {difficultySuffix}"),
                NewQuestion("Database: what is the difference between SQL and NoSQL, and when would you choose each?", "database", "short", $"Relational vs document model, schema, transactions, flexibility, use cases. {difficultySuffix}"),
                NewQuestion("Database: how would you optimize a slow query?", "database", "short", $"Indexes, query plan, projection, pagination, N+1, measurement. {difficultySuffix}"),
                NewQuestion("Security: how do you design secure authentication and authorization?", "security", "short", $"Password hashing, RBAC, token/session expiry, least privilege. {difficultySuffix}"),
                NewQuestion("Debugging: an API returns 500 in production. What steps do you take?", "debugging", "short", $"Logs, repro, request data, dependency checks, config, database, rollback. {difficultySuffix}"),
                NewQuestion("Testing: what backend tests give you confidence before release?", "testing", "short", $"Unit, integration, contract, auth, persistence, failure paths. {difficultySuffix}"),
                NewQuestion("Reliability: how would you handle timeout, retry, and duplicate request problems?", "reliability", "short", $"Timeouts, retries with backoff, idempotency, circuit breakers, observability. {difficultySuffix}"),
                NewQuestion("Observability: what logs or metrics would you add for a critical API?", "debugging", "short", $"Request id, latency, failures, dependency timing, business events. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this backend role? Aim for 60 seconds.", "career", "medium", $"Role fit, confidence, strongest evidence, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("frontend") || roleLower.Contains("ui") || roleLower.Contains("react"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your frontend development experience. Aim for 60-90 seconds.", "career", "medium", $"Education, frontend experience, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one UI feature you built. Cover the problem, components, state, API, and result.", "project", "long", $"Project context, ownership, components, state, API interaction, outcome. {difficultySuffix}"),
                NewQuestion("Coding: how would you remove duplicate items from a list before rendering it?", "coding", "short", $"Set/map approach, immutability, complexity, edge cases. {difficultySuffix}"),
                NewQuestion("Coding: how would you debounce a search input?", "coding", "short", $"Timer, cleanup, async request handling, UX. {difficultySuffix}"),
                NewQuestion("Components: how do you split a large component into smaller reusable components?", "frontend", "short", $"Responsibilities, props/state, composition, testability. {difficultySuffix}"),
                NewQuestion("State: when should state be local, and when should it be shared globally?", "frontend", "short", $"Scope, complexity, predictability, performance. {difficultySuffix}"),
                NewQuestion("API: how do you handle loading, empty, error, and success states in the UI?", "frontend", "short", $"UX states, retries, clear messages, resilience. {difficultySuffix}"),
                NewQuestion("Forms: how do you validate user input before and after an API call?", "frontend", "short", $"Client validation, server errors, disabled states, accessible messages. {difficultySuffix}"),
                NewQuestion("Performance: how do you improve a slow page?", "frontend", "short", $"Profiling, rendering, code splitting, memoization, network, assets. {difficultySuffix}"),
                NewQuestion("Accessibility: what checks do you make before releasing a screen?", "frontend", "short", $"Keyboard, semantic controls, ARIA, contrast, screen reader basics. {difficultySuffix}"),
                NewQuestion("Testing: what UI tests give you confidence before release?", "testing", "short", $"Component, interaction, regression, accessibility, API mocks. {difficultySuffix}"),
                NewQuestion("Debugging: a UI shows stale data after saving. How would you trace and fix it?", "debugging", "short", $"State updates, cache invalidation, API response, re-render path. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this frontend role? Aim for 60 seconds.", "career", "medium", $"Role fit, confidence, strongest evidence, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("mobile") || roleLower.Contains("android") || roleLower.Contains("ios") || roleLower.Contains("flutter") || roleLower.Contains("react native"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your mobile app development experience. Aim for 60-90 seconds.", "career", "medium", $"Education, mobile stack, app experience, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one mobile app feature you built. Cover UI, state, API/storage, and result.", "project", "long", $"Project context, ownership, UI flow, data handling, release/result. {difficultySuffix}"),
                NewQuestion("Coding: how would you prevent duplicate items in a mobile list before rendering?", "coding", "short", $"Set/map approach, stable ids, complexity, edge cases. {difficultySuffix}"),
                NewQuestion("State: how do you manage loading, offline, error, and success states in a mobile screen?", "frontend", "short", $"State model, local persistence, retry UX, network resilience. {difficultySuffix}"),
                NewQuestion("API: how should a mobile app handle slow networks and failed requests?", "api", "short", $"Timeouts, retries, caching, user feedback, idempotency. {difficultySuffix}"),
                NewQuestion("Storage: when would you use local storage, SQLite, or secure storage on a device?", "database", "short", $"Use cases, security, sync, data lifecycle. {difficultySuffix}"),
                NewQuestion("Security: how would you protect tokens and sensitive user data on a mobile device?", "security", "short", $"Secure storage, token expiry, transport security, least data. {difficultySuffix}"),
                NewQuestion("Performance: how would you diagnose a screen that scrolls or renders slowly?", "debugging", "short", $"Profiling, rendering, image size, list virtualization, API latency. {difficultySuffix}"),
                NewQuestion("Testing: what mobile tests would you run before releasing to users?", "testing", "short", $"Unit, UI, device matrix, network states, regression, store readiness. {difficultySuffix}"),
                NewQuestion("Architecture: how would you structure a mobile app so UI, state, services, and storage stay maintainable?", "technical", "short", $"Layering, dependency boundaries, state ownership, testability. {difficultySuffix}"),
                NewQuestion("Release quality: what would you monitor after a mobile app update goes live?", "reliability", "short", $"Crash rate, ANR, API errors, adoption, user feedback, rollback/hotfix plan. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this mobile role? Aim for 60 seconds.", "career", "medium", $"Role fit, app evidence, quality mindset, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("qa") || roleLower.Contains("quality") || roleLower.Contains("test"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your QA/testing experience. Aim for 60-90 seconds.", "career", "medium", $"Testing exposure, tools, quality mindset, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one feature you tested. Cover requirement analysis, test cases, bugs found, and release impact.", "project", "long", $"Project context, ownership, test design, defect quality, release confidence. {difficultySuffix}"),
                NewQuestion("Testing: how do you design test cases for a login page?", "testing", "short", $"Positive, negative, boundary, security, usability, compatibility. {difficultySuffix}"),
                NewQuestion("Testing: what is the difference between smoke, sanity, regression, and exploratory testing?", "testing", "short", $"Definitions, timing, purpose, release usage. {difficultySuffix}"),
                NewQuestion("Bug quality: what information belongs in a good bug report?", "testing", "short", $"Steps, expected/actual, environment, evidence, severity, priority. {difficultySuffix}"),
                NewQuestion("API testing: how would you test a candidate creation endpoint?", "api", "short", $"Payload validation, status codes, auth, persistence, duplicate cases. {difficultySuffix}"),
                NewQuestion("Automation: when would you automate a test, and when would you keep it manual?", "testing", "short", $"Repeatability, stability, ROI, exploratory value, maintenance. {difficultySuffix}"),
                NewQuestion("Debugging: a bug appears only in production. How do you help the team investigate it?", "debugging", "short", $"Repro, logs, environment, data, screenshots, triage. {difficultySuffix}"),
                NewQuestion("Release confidence: what checks should pass before sign-off?", "testing", "short", $"Critical flows, regression, performance basics, known risks, rollback readiness. {difficultySuffix}"),
                NewQuestion("Risk-based testing: how do you decide which scenarios to test first when time is limited?", "testing", "short", $"Business impact, critical paths, recent changes, defect history, risk. {difficultySuffix}"),
                NewQuestion("Test data: how do you prepare reliable data for repeatable manual or automated tests?", "database", "short", $"Known fixtures, cleanup, isolation, privacy, edge cases. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this QA role? Aim for 60 seconds.", "career", "medium", $"Role fit, quality mindset, evidence, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("devops") || roleLower.Contains("cloud") || roleLower.Contains("sre"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your DevOps/cloud experience. Aim for 60-90 seconds.", "career", "medium", $"Cloud, CI/CD, deployment, monitoring exposure, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one deployment or infrastructure project you worked on. Cover goal, pipeline/cloud design, risk, and result.", "project", "long", $"Project context, ownership, automation, monitoring, release result. {difficultySuffix}"),
                NewQuestion("CI/CD: what stages would you include in a safe deployment pipeline?", "reliability", "short", $"Build, test, scan, package, deploy, smoke test, rollback. {difficultySuffix}"),
                NewQuestion("Cloud: how would you design a basic scalable web app deployment?", "technical", "short", $"Compute, networking, database, storage, load balancing, secrets, monitoring. {difficultySuffix}"),
                NewQuestion("Docker: why use containers, and what belongs in a good Dockerfile?", "technical", "short", $"Consistency, image size, layers, security, config. {difficultySuffix}"),
                NewQuestion("Monitoring: which logs and metrics matter for a production API?", "debugging", "short", $"Latency, errors, saturation, request ids, dependency timing, alerts. {difficultySuffix}"),
                NewQuestion("Incident: an app is down after deployment. What do you do first?", "debugging", "short", $"Triage, rollback, logs, health checks, communication, root cause. {difficultySuffix}"),
                NewQuestion("Security: how should secrets be handled in deployment environments?", "security", "short", $"Secret managers, environment separation, rotation, least privilege. {difficultySuffix}"),
                NewQuestion("Reliability: how do retries, timeouts, and health checks help production stability?", "reliability", "short", $"Timeouts, backoff, idempotency, readiness/liveness, alerting. {difficultySuffix}"),
                NewQuestion("Infrastructure as code: why use it, and how does it improve release safety?", "reliability", "short", $"Repeatability, review, versioning, drift detection, rollback. {difficultySuffix}"),
                NewQuestion("Cost and scale: how would you reduce cloud cost without hurting reliability?", "technical", "short", $"Right sizing, autoscaling, reserved capacity, metrics, tradeoffs. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this DevOps/cloud role? Aim for 60 seconds.", "career", "medium", $"Role fit, reliability mindset, evidence, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("data analyst") || roleLower.Contains("analytics") || roleLower.Contains("bi "))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your data analysis experience. Aim for 60-90 seconds.", "career", "medium", $"Education, analytics tools, business communication, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one analysis project. Cover the business question, data preparation, analysis, insight, and impact.", "project", "long", $"Project context, ownership, data cleaning, analysis, recommendation/result. {difficultySuffix}"),
                NewQuestion("SQL: how would you find duplicate rows or repeated customers in a table?", "database", "short", $"GROUP BY, HAVING, keys, validation. {difficultySuffix}"),
                NewQuestion("SQL: explain joins with a practical example.", "database", "short", $"Inner/left joins, keys, row counts, pitfalls. {difficultySuffix}"),
                NewQuestion("Data quality: what checks do you run before trusting a dataset?", "testing", "short", $"Missing values, duplicates, outliers, types, consistency, source validation. {difficultySuffix}"),
                NewQuestion("Metrics: how do you define a good KPI for a business dashboard?", "technical", "short", $"Business goal, clear definition, denominator, refresh cadence, actionability. {difficultySuffix}"),
                NewQuestion("Visualization: how do you choose the right chart for an insight?", "frontend", "short", $"Audience, comparison/trend/distribution, simplicity, clarity. {difficultySuffix}"),
                NewQuestion("Stakeholders: how do you explain a technical analysis to a non-technical manager?", "behavioral", "medium", $"Story, assumptions, evidence, recommendation, limitations. {difficultySuffix}"),
                NewQuestion("Debugging: a dashboard number looks wrong. How do you investigate?", "debugging", "short", $"Definition, filters, joins, source data, refresh, reconciliation. {difficultySuffix}"),
                NewQuestion("Statistics: how would you compare two groups before recommending a business decision?", "technical", "short", $"Baseline, sample size, distribution, significance, practical impact. {difficultySuffix}"),
                NewQuestion("Data pipeline: what should be checked before refreshing a production report?", "reliability", "short", $"Source freshness, schema changes, row counts, validation, alerts. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this data analyst role? Aim for 60 seconds.", "career", "medium", $"Role fit, analytical evidence, communication, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("machine learning") || roleLower.Contains("ml ") || roleLower.Contains("ai engineer"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your machine learning experience. Aim for 60-90 seconds.", "career", "medium", $"ML education/projects, tools, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one ML project. Cover data, model choice, evaluation, deployment or result.", "project", "long", $"Project context, data prep, model, metrics, result. {difficultySuffix}"),
                NewQuestion("ML basics: how do you handle missing values and outliers?", "technical", "short", $"Imputation, removal, domain review, leakage prevention. {difficultySuffix}"),
                NewQuestion("Evaluation: how do you choose metrics for classification or regression?", "technical", "short", $"Accuracy, precision, recall, F1, ROC, MAE/RMSE, business cost. {difficultySuffix}"),
                NewQuestion("Overfitting: what causes it and how do you reduce it?", "technical", "short", $"Train/test split, validation, regularization, simpler model, data. {difficultySuffix}"),
                NewQuestion("Data leakage: what is it, and how do you prevent it?", "security", "short", $"Leakage examples, split timing, feature review, pipeline discipline. {difficultySuffix}"),
                NewQuestion("Deployment: what checks are needed before serving an ML model?", "reliability", "short", $"Latency, drift, monitoring, fallback, versioning, explainability. {difficultySuffix}"),
                NewQuestion("Debugging: model accuracy dropped after new data arrived. What do you inspect?", "debugging", "short", $"Data distribution, labels, pipeline, drift, metrics by segment. {difficultySuffix}"),
                NewQuestion("Communication: how do you explain model limitations to stakeholders?", "behavioral", "medium", $"Assumptions, confidence, risk, examples, decision guidance. {difficultySuffix}"),
                NewQuestion("Feature engineering: how do you decide which features to create or remove?", "technical", "short", $"Domain signal, leakage risk, validation, importance, simplicity. {difficultySuffix}"),
                NewQuestion("MLOps: how would you monitor model drift after deployment?", "reliability", "short", $"Input drift, prediction drift, labels, alerts, retraining, rollback. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this ML role? Aim for 60 seconds.", "career", "medium", $"Role fit, project evidence, learning mindset, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("cyber") || roleLower.Contains("security"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your cybersecurity experience. Aim for 60-90 seconds.", "career", "medium", $"Security exposure, tools, risk mindset, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one security-related project or lab. Cover the risk, your analysis, mitigation, and result.", "project", "long", $"Risk context, ownership, controls, evidence, outcome. {difficultySuffix}"),
                NewQuestion("Web security: what are common OWASP risks you would check first?", "security", "short", $"Injection, auth, access control, XSS, misconfiguration, logging. {difficultySuffix}"),
                NewQuestion("Authentication: how would you secure login and session handling?", "security", "short", $"Password hashing, MFA, session expiry, secure cookies/tokens, rate limits. {difficultySuffix}"),
                NewQuestion("Authorization: how do you prevent users from accessing another user's data?", "security", "short", $"Server-side checks, RBAC/ABAC, object-level authorization, tests. {difficultySuffix}"),
                NewQuestion("Incident: suspicious activity appears in logs. What do you do first?", "debugging", "short", $"Triage, preserve evidence, scope, contain, communicate, review logs. {difficultySuffix}"),
                NewQuestion("Vulnerability management: how do you prioritize and remediate findings?", "security", "short", $"Severity, exploitability, asset criticality, patching, validation. {difficultySuffix}"),
                NewQuestion("Cloud/security: how would you protect secrets and production configuration?", "security", "short", $"Secret manager, rotation, least privilege, environment isolation. {difficultySuffix}"),
                NewQuestion("Monitoring: what security events should be logged and alerted?", "debugging", "short", $"Failed login spikes, privilege changes, sensitive access, anomalies. {difficultySuffix}"),
                NewQuestion("Secure coding: how would you reduce injection risk in an application?", "security", "short", $"Parameterized queries, validation, encoding, least privilege, tests. {difficultySuffix}"),
                NewQuestion("Risk communication: how do you explain a security finding to a business stakeholder?", "behavioral", "medium", $"Impact, likelihood, evidence, remediation options, urgency. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this cybersecurity role? Aim for 60 seconds.", "career", "medium", $"Role fit, risk mindset, evidence, job focus: {jobFocus}."),
            ];
        }

        if (roleLower.Contains("database") || roleLower.Contains("dba"))
        {
            return
            [
                NewQuestion($"Tell me about yourself and your database administration experience. Aim for 60-90 seconds.", "career", "medium", $"Database exposure, operations, performance, role alignment. Job focus: {jobFocus}."),
                NewQuestion("Walk me through one database problem you solved. Cover symptoms, investigation, fix, and result.", "project", "long", $"Problem context, query/storage analysis, action, measured result. {difficultySuffix}"),
                NewQuestion("Database: what is an index, and how do you decide which index to add?", "database", "short", $"Query pattern, selectivity, explain plan, write tradeoff. {difficultySuffix}"),
                NewQuestion("SQL: how would you diagnose a slow query?", "database", "short", $"Execution plan, indexes, joins, scans, statistics, pagination. {difficultySuffix}"),
                NewQuestion("Backup/recovery: what makes a backup strategy reliable?", "reliability", "short", $"RPO/RTO, restore testing, automation, retention, monitoring. {difficultySuffix}"),
                NewQuestion("Security: how do you protect database access and sensitive data?", "security", "short", $"Least privilege, encryption, auditing, secrets, network rules. {difficultySuffix}"),
                NewQuestion("Data integrity: how do transactions and constraints help correctness?", "database", "short", $"ACID, constraints, isolation, consistency, rollback. {difficultySuffix}"),
                NewQuestion("Monitoring: what database metrics would you watch in production?", "debugging", "short", $"CPU, memory, locks, slow queries, connections, replication, storage. {difficultySuffix}"),
                NewQuestion("Migration: how would you roll out a schema change safely?", "reliability", "short", $"Backward compatibility, backups, staged deploy, rollback, validation. {difficultySuffix}"),
                NewQuestion("Replication: how does replication improve availability, and what can still go wrong?", "reliability", "short", $"Primary/replica, failover, lag, consistency, monitoring. {difficultySuffix}"),
                NewQuestion("Capacity planning: how would you know a database needs scaling or archiving?", "technical", "short", $"Growth rate, storage, latency, throughput, retention, cost. {difficultySuffix}"),
                NewQuestion("Final: why should we hire you for this DBA role? Aim for 60 seconds.", "career", "medium", $"Role fit, reliability mindset, evidence, job focus: {jobFocus}."),
            ];
        }

        return
        [
            NewQuestion($"Tell me about yourself and your experience for the {role} role. Aim for 60-90 seconds.", "career", "medium", $"Education, work experience, role alignment. Job focus: {jobFocus}."),
            NewQuestion("Tell me about a project you are proud of. Cover the problem, your role, technology used, and result.", "project", "long", $"Problem breakdown, action, tools, collaboration, measurable impact. {difficultySuffix}"),
            NewQuestion("Coding: how do you break down a problem before writing code?", "coding", "short", $"Inputs, outputs, constraints, edge cases, steps. {difficultySuffix}"),
            NewQuestion("Coding: how would you debug a function or feature returning the wrong output?", "coding", "short", $"Reproduce, isolate, inspect data, test fix. {difficultySuffix}"),
            NewQuestion("Coding: which data structure would you use to check duplicates quickly, and why?", "coding", "short", $"Hash set/map, complexity, memory tradeoff, edge cases. {difficultySuffix}"),
            NewQuestion("OOP: explain encapsulation, inheritance, polymorphism, and abstraction with simple examples.", "oop", "short", $"OOP principles, examples, object responsibility. {difficultySuffix}"),
            NewQuestion("API: what should a good API response include when validation fails?", "api", "short", $"Status code, message, field errors, consistency, security. {difficultySuffix}"),
            NewQuestion("Database: what is an index, and how does it improve search performance?", "database", "short", $"Index purpose, query performance, tradeoff, measurement. {difficultySuffix}"),
            NewQuestion("Security: what basic checks should every application have?", "security", "short", $"Authentication, authorization, validation, secrets, logging. {difficultySuffix}"),
            NewQuestion("Debugging: a feature works on your machine but fails for users. What do you inspect?", "debugging", "short", $"Reproduction, logs, environment, config, dependencies, data. {difficultySuffix}"),
            NewQuestion("Testing: how do you ensure quality before release?", "testing", "short", $"Testing, review, monitoring, rollback plan. {difficultySuffix}"),
            NewQuestion("Behavioral: tell me about a challenge or bug you solved and what you learned.", "behavioral", "medium", $"Situation, action, technical detail, outcome, learning. {difficultySuffix}"),
            NewQuestion("Final: why should we hire you for this role? Aim for 60 seconds.", "career", "medium", $"Role fit, confidence, evidence, growth intent."),
        ];
    }

    private static IReadOnlyList<InterviewQuestion> BuildQuestionPlan(
        IReadOnlyList<InterviewQuestion> pool,
        InterviewCategory category,
        int count)
    {
        var targetCount = Math.Max(1, count);
        if (pool.Count == 0)
        {
            return [];
        }

        var intro = pool.FirstOrDefault(q => IsSection(q, "career")) ?? pool[0];
        var final = pool.LastOrDefault(q => q.Prompt.StartsWith("Final:", StringComparison.OrdinalIgnoreCase));
        var plan = new List<InterviewQuestion> { intro };
        var middle = pool
            .Where(q => !ReferenceEquals(q, intro) && (final is null || !ReferenceEquals(q, final)))
            .ToList();

        if (category == InterviewCategory.Technical)
        {
            var sectionOrder = new[]
            {
                "project", "coding", "coding", "oop", "api", "database", "security", "debugging", "testing", "frontend", "reliability", "behavioral", "technical"
            };

            foreach (var section in sectionOrder)
            {
                if (plan.Count >= targetCount - 1 || middle.Count == 0)
                {
                    break;
                }

                var matches = middle.Where(q => IsSection(q, section)).ToList();
                if (matches.Count == 0)
                {
                    continue;
                }

                var selected = PickRandom(matches);
                plan.Add(selected);
                middle.Remove(selected);
            }
        }

        while (plan.Count < targetCount - 1 && middle.Count > 0)
        {
            var selected = PickRandom(middle);
            plan.Add(selected);
            middle.Remove(selected);
        }

        if (final is not null && plan.Count < targetCount && !ReferenceEquals(final, intro))
        {
            plan.Add(final);
        }

        return plan.Take(targetCount).ToList();
    }

    private static InterviewQuestion PickRandom(IReadOnlyList<InterviewQuestion> questions)
    {
        return questions[Random.Shared.Next(questions.Count)];
    }

    private static bool IsSection(InterviewQuestion question, string section)
    {
        return ExtractHintValue(question.IdealAnswerHint, "Section").Equals(section, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractHintValue(string? hint, string key)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return string.Empty;
        }

        foreach (var part in hint.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = part[..separatorIndex].Trim();
            if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return part[(separatorIndex + 1)..].Trim();
            }
        }

        return string.Empty;
    }

    private static InterviewQuestion NewQuestion(string prompt, string section, string expectedAnswer, string hint)
    {
        return new InterviewQuestion
        {
            Prompt = prompt,
            IdealAnswerHint = $"Section:{section}; Expected:{expectedAnswer}; {hint}",
        };
    }

    private static string BuildJobFocus(string jobDescription)
    {
        if (string.IsNullOrWhiteSpace(jobDescription))
        {
            return "role fundamentals";
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "with", "from", "that", "this", "will", "have", "your", "role", "work", "team", "good",
            "must", "should", "need", "needs", "using", "about", "and", "the", "for", "you", "our"
        };

        var terms = jobDescription
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '-', '_', '\n', '\r', '\t', '(', ')', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 2 && !stopWords.Contains(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        return terms.Length == 0 ? "role fundamentals" : string.Join(", ", terms);
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
