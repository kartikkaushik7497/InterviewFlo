using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Infrastructure.Configuration;

namespace MockUpAi.Infrastructure.Services;

internal sealed class OpenAiInterviewTurnOrchestrator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiSettings _settings;
    private readonly ISecretVaultService _secretVault;
    private readonly ILogger<OpenAiInterviewTurnOrchestrator> _logger;
    private readonly HeuristicInterviewTurnOrchestrator _fallback;

    public OpenAiInterviewTurnOrchestrator(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiSettings> settings,
        ISecretVaultService secretVault,
        ILogger<OpenAiInterviewTurnOrchestrator> logger,
        HeuristicInterviewTurnOrchestrator fallback)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _secretVault = secretVault;
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
            var apiKey = await ResolveApiKeyAsync(cancellationToken);
            var client = _httpClientFactory.CreateClient(nameof(OpenAiInterviewTurnOrchestrator));
            client.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var history = string.Join("\n", session.ConversationTurns.TakeLast(10).Select(x => $"{x.Speaker} ({x.TurnType}): {x.Text}"));
            var completedTopics = string.Join(", ", session.QuestionResults.Select(x => x.Topic).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
            var strengths = string.Join("; ", session.QuestionResults.Select(x => x.Strengths).Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(5));
            var gaps = string.Join("; ", session.QuestionResults.Select(x => x.Gaps).Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(5));

            var systemPrompt = """
You are InterviewFlo, an experienced human technical interviewer.
Be calm, concise, fair, adaptive, and conversational.
Do not reveal scores to the candidate during the interview.
Do not praise weak, vague, offensive, or irrelevant answers.
Ask only one question at a time.
If the answer is vague, ask one clarification before moving on.
If the answer is inappropriate, acknowledge professionally, score low, and continue.
Prefer concrete examples, implementation details, tradeoffs, and measurable outcomes.
Return JSON only.
""";

            var userPrompt = $$"""
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

Completed topics:
{{completedTopics}}

Recent conversation:
{{history}}

Observed strengths:
{{strengths}}

Observed gaps:
{{gaps}}

Return strict JSON:
{
  "acknowledgement": "voice-friendly sentence",
  "shouldAskClarification": true,
  "clarificationPrompt": "question or null",
  "shouldMoveToNewTopic": false,
  "nextQuestion": "single next interviewer question",
  "idealAnswerHint": "evaluation signals for the next question",
  "topic": "topic name",
  "technicalScore": 0,
  "communicationScore": 0,
  "depthScore": 0,
  "relevanceScore": 0,
  "problemSolvingScore": 0,
  "overallScore": 0,
  "evaluationSummary": "short internal summary",
  "observedStrengths": ["strength"],
  "observedGaps": ["gap"]
}
""";

            var payload = new
            {
                model = _settings.Model,
                temperature = 0.25,
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
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var parsed = JsonSerializer.Deserialize<InterviewTurnDecision>(SanitizeJson(content ?? string.Empty), JsonOptions());
            return Normalize(parsed, session, currentQuestionIndex, totalQuestionCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI turn orchestration failed. Falling back to heuristic turn orchestration.");
            return await _fallback.DecideNextTurnAsync(session, currentQuestion, candidateAnswer, currentQuestionIndex, totalQuestionCount, cancellationToken);
        }
    }

    private async Task<string> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return _settings.ApiKey.Trim();
        }

        var key = await _secretVault.GetOpenAiApiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        throw new InvalidOperationException("OpenAI API key is missing.");
    }

    private static InterviewTurnDecision Normalize(InterviewTurnDecision? decision, InterviewSession session, int index, int total)
    {
        if (decision is null || string.IsNullOrWhiteSpace(decision.NextQuestion))
        {
            return new InterviewTurnDecision
            {
                Acknowledgement = "Thanks. Let us continue with the next area.",
                NextQuestion = $"For the {session.JobRole} role, walk me through one practical decision you would make in production.",
                IdealAnswerHint = "Concrete decision, rationale, tradeoff, and expected outcome.",
                Topic = "production decision making",
                OverallScore = 40,
            };
        }

        var overall = decision.OverallScore > 0
            ? decision.OverallScore
            : HeuristicInterviewTurnOrchestrator.Weighted(decision.TechnicalScore, decision.DepthScore, decision.RelevanceScore, decision.CommunicationScore, decision.ProblemSolvingScore);

        return new InterviewTurnDecision
        {
            Acknowledgement = string.IsNullOrWhiteSpace(decision.Acknowledgement) ? "Thanks, that helps." : decision.Acknowledgement.Trim(),
            ClarificationPrompt = decision.ClarificationPrompt,
            NextQuestion = decision.NextQuestion.Trim(),
            IdealAnswerHint = string.IsNullOrWhiteSpace(decision.IdealAnswerHint) ? "Relevant, concrete, role-specific answer." : decision.IdealAnswerHint.Trim(),
            Topic = string.IsNullOrWhiteSpace(decision.Topic) ? $"question {Math.Min(index + 2, total)}" : decision.Topic.Trim(),
            ShouldAskClarification = decision.ShouldAskClarification,
            ShouldMoveToNewTopic = decision.ShouldMoveToNewTopic,
            TechnicalScore = Math.Clamp(decision.TechnicalScore, 0, 100),
            CommunicationScore = Math.Clamp(decision.CommunicationScore, 0, 100),
            DepthScore = Math.Clamp(decision.DepthScore, 0, 100),
            RelevanceScore = Math.Clamp(decision.RelevanceScore, 0, 100),
            ProblemSolvingScore = Math.Clamp(decision.ProblemSolvingScore, 0, 100),
            OverallScore = Math.Round(Math.Clamp(overall, 0, 100), 2),
            EvaluationSummary = decision.EvaluationSummary.Trim(),
            ObservedStrengths = decision.ObservedStrengths,
            ObservedGaps = decision.ObservedGaps,
        };
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
