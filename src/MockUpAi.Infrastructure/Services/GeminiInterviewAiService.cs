using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;
using MockUpAi.Infrastructure.Configuration;

namespace MockUpAi.Infrastructure.Services;

internal sealed class GeminiInterviewAiService : IInterviewAiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly HeuristicInterviewAiService _fallback;
    private readonly ILogger<GeminiInterviewAiService> _logger;

    public GeminiInterviewAiService(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiSettings> settings,
        ILogger<GeminiInterviewAiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
        _fallback = new HeuristicInterviewAiService();
    }

    public async Task<IReadOnlyList<InterviewQuestion>> GenerateQuestionsAsync(InterviewAiProvider provider, string jobRole, InterviewCategory category, string jobDescription, InterviewDifficulty difficulty, int count, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
Create {{count}} interview questions for role: {{jobRole}}.
Interview category: {{category}}.
Difficulty level: {{difficulty}}.
Job description:
{{jobDescription}}

Rules:
- Build a structured interview plan, not random theory questions.
- Ask exactly one intro question first.
- If category is Technical, every question after the intro should be direct and practical: project walkthrough, coding, OOP/design, API, database, security, debugging, testing, and final fit.
- If category is Behavioral, HR, or Management, adapt the sections to that category instead of forcing technical questions.
- Vary the wording and topics for each generated interview so the same role does not always receive the exact same list.
- Avoid vague standalone prompts like "discuss a tradeoff" unless tied to a concrete project or system design scenario.
- Do not ask abstract random theory; every question should sound like a real interviewer would ask it.
- Include these sections when category is Technical: career/education, project, coding, OOP/design, role-specific practical topics, testing/debugging, final fit.
- Keep coding/OOP/API/database questions short and direct.
- Career or project questions may request a medium-long answer and should say that in the prompt.
- Every idealAnswerHint must start with: Section:<section>; Expected:<short|medium|long>;
- Match questions to the role and job description.

Return strict JSON array only:
[
  {
    "prompt": "string",
    "idealAnswerHint": "string"
  }
]
""";

        try
        {
            var content = await SendPromptAsync(prompt, cancellationToken);
            var questions = JsonSerializer.Deserialize<List<GeminiQuestion>>(SanitizeJson(content), JsonOptions());
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
            _logger.LogWarning(ex, "Gemini question generation failed. Falling back to heuristic mode.");
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
        var prompt = $$"""
Generate a short professional interviewer introduction for voice.
Candidate: {{candidateName}}
Role: {{jobRole}}
Category: {{category}}
Difficulty: {{difficulty}}
One sentence, under 24 words. Do not include a paragraph, bullet list, or line break.
""";

        try
        {
            return (await SendPromptAsync(prompt, cancellationToken)).Trim();
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
        var prompt = $$"""
Generate one supportive professional line after this answer:
{{candidateTranscript}}
Max 20 words.
""";
        try
        {
            return (await SendPromptAsync(prompt, cancellationToken)).Trim();
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
        var historyPreview = string.Join("\n", conversationTurns.TakeLast(6).Select(x => $"{x.Speaker}: {x.Text}"));
        var prompt = $$"""
Generate one follow-up interview question in JSON.
Role: {{jobRole}}
Category: {{category}}
Difficulty: {{difficulty}}
Previous question: {{previousQuestion.Prompt}}
Candidate answer: {{candidateTranscript}}
Recent conversation:
{{historyPreview}}

Return strict JSON:
{
  "prompt": "string",
  "idealAnswerHint": "string"
}
""";

        try
        {
            var content = await SendPromptAsync(prompt, cancellationToken);
            var parsed = JsonSerializer.Deserialize<GeminiQuestion>(SanitizeJson(content), JsonOptions());
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Prompt))
            {
                return await _fallback.GenerateFollowUpQuestionAsync(provider, jobRole, category, difficulty, previousQuestion, candidateTranscript, conversationTurns, cancellationToken);
            }

            return new InterviewQuestion
            {
                Prompt = parsed.Prompt.Trim(),
                IdealAnswerHint = (parsed.IdealAnswerHint ?? string.Empty).Trim(),
            };
        }
        catch
        {
            return await _fallback.GenerateFollowUpQuestionAsync(provider, jobRole, category, difficulty, previousQuestion, candidateTranscript, conversationTurns, cancellationToken);
        }
    }

    public async Task<InterviewAnswerEvaluation> EvaluateAnswerAsync(InterviewAiProvider provider, string jobRole, InterviewDifficulty difficulty, InterviewQuestion question, string candidateTranscript, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
Evaluate answer for role: {{jobRole}}, difficulty: {{difficulty}}.

Question:
{{question.Prompt}}

Ideal answer signals:
{{question.IdealAnswerHint}}

Candidate transcript:
{{candidateTranscript}}

Return strict JSON:
{
  "score": 0-100,
  "feedback": "short actionable feedback"
}
""";

        try
        {
            var content = await SendPromptAsync(prompt, cancellationToken);
            var eval = JsonSerializer.Deserialize<GeminiEvaluation>(SanitizeJson(content), JsonOptions());
            if (eval is null)
            {
                return await _fallback.EvaluateAnswerAsync(provider, jobRole, difficulty, question, candidateTranscript, cancellationToken);
            }

            var score = Math.Round(Math.Clamp(eval.Score, 0, 100), 2);
            return new InterviewAnswerEvaluation(score >= 60, score, string.IsNullOrWhiteSpace(eval.Feedback) ? "Feedback unavailable." : eval.Feedback.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini evaluation failed. Falling back to heuristic mode.");
            return await _fallback.EvaluateAnswerAsync(provider, jobRole, difficulty, question, candidateTranscript, cancellationToken);
        }
    }

    public async Task<double> CalculateRoleFitScoreAsync(InterviewAiProvider provider, string jobRole, string jobDescription, InterviewDifficulty difficulty, IReadOnlyList<InterviewQuestionResult> results, CancellationToken cancellationToken = default)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        var prompt = $$"""
Estimate role fit score (0-100) for role {{jobRole}}, difficulty {{difficulty}}.
Job description:
{{jobDescription}}

Interview results JSON:
{{JsonSerializer.Serialize(results)}}

Return strict JSON:
{
  "roleFitScore": 0-100
}
""";

        try
        {
            var content = await SendPromptAsync(prompt, cancellationToken);
            var eval = JsonSerializer.Deserialize<GeminiFit>(SanitizeJson(content), JsonOptions());
            if (eval is null)
            {
                return await _fallback.CalculateRoleFitScoreAsync(provider, jobRole, jobDescription, difficulty, results, cancellationToken);
            }

            return Math.Round(Math.Clamp(eval.RoleFitScore, 0, 100), 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini fit score failed. Falling back to heuristic mode.");
            return await _fallback.CalculateRoleFitScoreAsync(provider, jobRole, jobDescription, difficulty, results, cancellationToken);
        }
    }

    private async Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is missing.");
        }

        var client = _httpClientFactory.CreateClient(nameof(GeminiInterviewAiService));
        var endpoint = $"{_settings.BaseUrl.TrimEnd('/')}/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json",
            },
        };

        using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini response content was empty.");
        }

        return text;
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };

    private static string SanitizeJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('`').Trim();
            var start = trimmed.IndexOfAny(['{', '[']);
            if (start >= 0)
            {
                trimmed = trimmed[start..];
            }
        }

        return trimmed;
    }

    private sealed class GeminiQuestion
    {
        public string? Prompt { get; set; }
        public string? IdealAnswerHint { get; set; }
    }

    private sealed class GeminiEvaluation
    {
        public double Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }

    private sealed class GeminiFit
    {
        public double RoleFitScore { get; set; }
    }
}
