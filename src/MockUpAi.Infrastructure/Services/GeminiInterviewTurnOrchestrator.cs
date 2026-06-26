using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Infrastructure.Configuration;

namespace MockUpAi.Infrastructure.Services;

internal sealed class GeminiInterviewTurnOrchestrator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiInterviewTurnOrchestrator> _logger;
    private readonly HeuristicInterviewTurnOrchestrator _fallback;

    public GeminiInterviewTurnOrchestrator(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiSettings> settings,
        ILogger<GeminiInterviewTurnOrchestrator> logger,
        HeuristicInterviewTurnOrchestrator fallback)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
        _fallback = fallback;
    }

    public async Task<InterviewTurnDecision> DecideNextTurnAsync(
        InterviewSession session,
        InterviewQuestion currentQuestion,
        string candidateAnswer,
        int currentQuestionIndex,
        int totalQuestionCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("Gemini API key is missing.");
            }

            var history = string.Join("\n", session.ConversationTurns.TakeLast(10).Select(x => $"{x.Speaker}: {x.Text}"));
            var prompt = $$"""
You are InterviewFlo, an experienced human interviewer. Return JSON only.
Be concise and adaptive. Ask one question at a time. Do not reveal scores.
If an answer is vague, ask one clarification. If it is irrelevant or inappropriate, score low and move on professionally.

Role: {{session.JobRole}}
Difficulty: {{session.Difficulty}}
Category: {{session.Category}}
Job description:
{{session.JobDescription}}

Question {{currentQuestionIndex + 1}} of {{totalQuestionCount}}:
{{currentQuestion.Prompt}}

Ideal answer signals:
{{currentQuestion.IdealAnswerHint}}

Candidate answer:
{{candidateAnswer}}

Recent conversation:
{{history}}

Return strict JSON with:
acknowledgement, shouldAskClarification, clarificationPrompt, shouldMoveToNewTopic, nextQuestion,
idealAnswerHint, topic, technicalScore, communicationScore, depthScore, relevanceScore,
problemSolvingScore, overallScore, evaluationSummary, observedStrengths, observedGaps.
""";

            var client = _httpClientFactory.CreateClient(nameof(GeminiInterviewTurnOrchestrator));
            var endpoint = $"{_settings.BaseUrl.TrimEnd('/')}/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.25, responseMimeType = "application/json" },
            };

            using var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            var parsed = JsonSerializer.Deserialize<InterviewTurnDecision>(SanitizeJson(text ?? string.Empty), JsonOptions());
            return parsed ?? await _fallback.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini turn orchestration failed. Falling back to heuristic turn orchestration.");
            return await _fallback.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken);
        }
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };

    private static string SanitizeJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('`').Trim();
            var start = trimmed.IndexOf('{');
            if (start >= 0)
            {
                trimmed = trimmed[start..];
            }
        }

        return trimmed;
    }
}
