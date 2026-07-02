using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InterviewFlo.Core.Application.Abstractions;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;
using InterviewFlo.Infrastructure.Configuration;

namespace InterviewFlo.Infrastructure.Services;

internal sealed class OpenAiInterviewAiService : IInterviewAiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiSettings _settings;
    private readonly HeuristicInterviewAiService _fallback;
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<OpenAiInterviewAiService> _logger;

    public OpenAiInterviewAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiSettings> settings,
        ISecretVaultService secretVault,
        ILogger<OpenAiInterviewAiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _secretVault = secretVault;
        _logger = logger;
        _fallback = new HeuristicInterviewAiService();
    }

    public async Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        string jobDescription,
        InterviewDifficulty difficulty,
        int count,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a senior technical interviewer. Output JSON only.";
        var userPrompt = $$"""
Create {{count}} interview questions for role: {{jobRole}}.
Interview category: {{category}}.
Difficulty level: {{difficulty}}.
Job description:
{{jobDescription}}

Rules:
- Mix conceptual, practical, and scenario-based questions.
- Keep questions specific to the role and job description.
- Every question must include a strict evaluation hint.
- Match the depth to this difficulty:
  - Fresher: fundamentals and beginner implementation
  - Experienced: practical debugging and design tradeoffs
  - Professional: architecture, scale, and leadership-level decisions

Output JSON array with this schema:
[
  {
    "prompt": "string",
    "idealAnswerHint": "string"
  }
]
""";

        try
        {
            var content = await SendPromptAsync(systemPrompt, userPrompt, cancellationToken);
            var questions = JsonSerializer.Deserialize<List<OpenAiQuestion>>(SanitizeJson(content), JsonOptions());

            if (questions is null || questions.Count == 0)
            {
                return await _fallback.GenerateQuestionsAsync(provider, jobRole, category, jobDescription, difficulty, count, cancellationToken);
            }

            return questions
                .Where(x => !string.IsNullOrWhiteSpace(x.Prompt))
                .Select(x => new InterviewQuestion
                {
                    Prompt = x.Prompt!.Trim(),
                    IdealAnswerHint = (x.IdealAnswerHint ?? string.Empty).Trim(),
                })
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI question generation failed. Falling back to heuristic mode.");
            return await _fallback.GenerateQuestionsAsync(provider, jobRole, category, jobDescription, difficulty, count, cancellationToken);
        }
    }

    public async Task<string> GenerateProfessionalIntroductionAsync(
        InterviewAiProvider provider,
        string candidateName,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a professional interviewer. Output plain text only.";
        var userPrompt = $$"""
Generate a short voice-friendly professional introduction for an AI interviewer.
Candidate: {{candidateName}}
Role: {{jobRole}}
Category: {{category}}
Difficulty: {{difficulty}}

Rules:
- Keep it under 55 words.
- Sound supportive and confident.
- Mention we will proceed one question at a time.
""";

        try
        {
            var content = await SendPromptAsync(systemPrompt, userPrompt, cancellationToken);
            return content.Trim();
        }
        catch
        {
            return await _fallback.GenerateProfessionalIntroductionAsync(provider, candidateName, jobRole, category, difficulty, cancellationToken);
        }
    }

    public async Task<string> GenerateEncouragementAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        string candidateTranscript,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a professional interviewer. Output plain text only.";
        var userPrompt = $$"""
Generate one short supportive phrase after a candidate answer.
Role: {{jobRole}}
Category: {{category}}
Difficulty: {{difficulty}}
Candidate answer:
{{candidateTranscript}}

Rules:
- One sentence, <= 20 words.
- Encouraging but professional.
- Do not praise blindly; encourage deeper clarity when needed.
""";

        try
        {
            var content = await SendPromptAsync(systemPrompt, userPrompt, cancellationToken);
            return content.Trim();
        }
        catch
        {
            return await _fallback.GenerateEncouragementAsync(provider, jobRole, category, difficulty, candidateTranscript, cancellationToken);
        }
    }

    public async Task<InterviewQuestion> GenerateFollowUpQuestionAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewCategory category,
        InterviewDifficulty difficulty,
        InterviewQuestion previousQuestion,
        string candidateTranscript,
        IReadOnlyList<InterviewConversationTurn> conversationTurns,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a professional interviewer. Output JSON only.";
        var historyPreview = string.Join("\n", conversationTurns.TakeLast(6).Select(x => $"{x.Speaker}: {x.Text}"));
        var userPrompt = $$"""
Generate one intelligent follow-up interview question.
Role: {{jobRole}}
Category: {{category}}
Difficulty: {{difficulty}}

Previous question:
{{previousQuestion.Prompt}}

Candidate answer:
{{candidateTranscript}}

Recent conversation:
{{historyPreview}}

Return JSON:
{
  "prompt": "string",
  "idealAnswerHint": "string"
}
""";

        try
        {
            var content = await SendPromptAsync(systemPrompt, userPrompt, cancellationToken);
            var result = JsonSerializer.Deserialize<OpenAiQuestion>(SanitizeJson(content), JsonOptions());
            if (result is null || string.IsNullOrWhiteSpace(result.Prompt))
            {
                return await _fallback.GenerateFollowUpQuestionAsync(provider, jobRole, category, difficulty, previousQuestion, candidateTranscript, conversationTurns, cancellationToken);
            }

            return new InterviewQuestion
            {
                Prompt = result.Prompt.Trim(),
                IdealAnswerHint = (result.IdealAnswerHint ?? string.Empty).Trim(),
            };
        }
        catch
        {
            return await _fallback.GenerateFollowUpQuestionAsync(provider, jobRole, category, difficulty, previousQuestion, candidateTranscript, conversationTurns, cancellationToken);
        }
    }

    public async Task<InterviewAnswerEvaluation> EvaluateAnswerAsync(
        InterviewAiProvider provider,
        string jobRole,
        InterviewDifficulty difficulty,
        InterviewQuestion question,
        string candidateTranscript,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = "You are a strict interview evaluator. Score based on evidence in candidate transcript only. Output JSON only.";
        var userPrompt = $$"""
Evaluate this answer for role: {{jobRole}} at difficulty: {{difficulty}}.

Question:
{{question.Prompt}}

Ideal answer signals:
{{question.IdealAnswerHint}}

Candidate transcript:
{{candidateTranscript}}

Rubric weights:
- Technical correctness: 45
- Role relevance: 20
- Depth and reasoning: 20
- Clarity and communication: 15

Return JSON:
{
  "isCorrect": true|false,
  "score": 0-100,
  "technicalCorrectness": 0-100,
  "roleRelevance": 0-100,
  "depth": 0-100,
  "clarity": 0-100,
  "feedback": "short actionable feedback"
}
""";

        try
        {
            var content = await SendPromptAsync(systemPrompt, userPrompt, cancellationToken);
            var evaluation = JsonSerializer.Deserialize<OpenAiEvaluation>(SanitizeJson(content), JsonOptions());

            if (evaluation is null)
            {
                return await _fallback.EvaluateAnswerAsync(provider, jobRole, difficulty, question, candidateTranscript, cancellationToken);
            }

            var weighted = WeightedScore(evaluation);
            var finalScore = Math.Round((evaluation.Score + weighted) / 2.0, 2);
            var isCorrect = finalScore >= 60 && evaluation.TechnicalCorrectness >= 50;
            var feedback = $"{evaluation.Feedback} [Tech:{evaluation.TechnicalCorrectness:0}, Role:{evaluation.RoleRelevance:0}, Depth:{evaluation.Depth:0}, Clarity:{evaluation.Clarity:0}]";

            return new InterviewAnswerEvaluation(isCorrect, Math.Clamp(finalScore, 0, 100), feedback.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI evaluation failed. Falling back to heuristic mode.");
            return await _fallback.EvaluateAnswerAsync(provider, jobRole, difficulty, question, candidateTranscript, cancellationToken);
        }
    }

    public async Task<double> CalculateRoleFitScoreAsync(
        InterviewAiProvider provider,
        string jobRole,
        string jobDescription,
        InterviewDifficulty difficulty,
        IReadOnlyList<InterviewQuestionResult> results,
        CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        var systemPrompt = "You are a hiring panel model. Output JSON only.";
        var userPrompt = $$"""
Estimate candidate role-fit score for role: {{jobRole}}
Difficulty level: {{difficulty}}
Job description:
{{jobDescription}}

Interview results JSON:
{{JsonSerializer.Serialize(results)}}

Role-fit rubric:
- Core technical alignment: 40
- Practical problem-solving quality: 25
- Communication and structure: 15
- Consistency across questions: 20

Return JSON:
{
  "roleFitScore": 0-100
}
""";

        try
        {
            var content = await SendPromptAsync(systemPrompt, userPrompt, cancellationToken);
            var fit = JsonSerializer.Deserialize<OpenAiFitScore>(SanitizeJson(content), JsonOptions());

            if (fit is null)
            {
                return await _fallback.CalculateRoleFitScoreAsync(provider, jobRole, jobDescription, difficulty, results, cancellationToken);
            }

            return Math.Round(Math.Clamp(fit.RoleFitScore, 0, 100), 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI role-fit calculation failed. Falling back to heuristic mode.");
            return await _fallback.CalculateRoleFitScoreAsync(provider, jobRole, jobDescription, difficulty, results, cancellationToken);
        }
    }

    private async Task<string> SendPromptAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var apiKey = await ResolveApiKeyAsync(cancellationToken);

        var client = _httpClientFactory.CreateClient(nameof(OpenAiInterviewAiService));
        client.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model = _settings.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
        };

        using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        var content = document
            .RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI response content was empty.");
        }

        return content;
    }

    private async Task<string> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return _settings.ApiKey;
        }

        var key = await _secretVault.GetOpenAiApiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        throw new InvalidOperationException("OpenAI API key is missing.");
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }

    private static string SanitizeJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('`').Trim();
            var firstBrace = trimmed.IndexOf('{');
            if (firstBrace >= 0)
            {
                trimmed = trimmed[firstBrace..];
            }
        }

        return trimmed;
    }

    private static double WeightedScore(OpenAiEvaluation evaluation)
    {
        return
            evaluation.TechnicalCorrectness * 0.45 +
            evaluation.RoleRelevance * 0.20 +
            evaluation.Depth * 0.20 +
            evaluation.Clarity * 0.15;
    }

    private sealed class OpenAiQuestion
    {
        public string? Prompt { get; set; }

        public string? IdealAnswerHint { get; set; }
    }

    private sealed class OpenAiEvaluation
    {
        public bool IsCorrect { get; set; }

        public double Score { get; set; }

        public double TechnicalCorrectness { get; set; }

        public double RoleRelevance { get; set; }

        public double Depth { get; set; }

        public double Clarity { get; set; }

        public string Feedback { get; set; } = string.Empty;
    }

    private sealed class OpenAiFitScore
    {
        public double RoleFitScore { get; set; }
    }
}
