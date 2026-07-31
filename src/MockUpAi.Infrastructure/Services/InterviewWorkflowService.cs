using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Infrastructure.Services;

internal sealed class InterviewWorkflowService : IInterviewWorkflowService
{
    private readonly IInterviewAiService _interviewAiService;
    private readonly IInterviewTurnOrchestrator _turnOrchestrator;
    private readonly IInterviewRepository _interviewRepository;

    private InterviewSession? _session;
    private readonly List<InterviewQuestion> _questions = [];
    private IReadOnlyList<InterviewQuestion> _seedQuestions = [];
    private int _currentIndex;
    private int _seedCursor;

    public InterviewWorkflowService(
        IInterviewAiService interviewAiService,
        IInterviewTurnOrchestrator turnOrchestrator,
        IInterviewRepository interviewRepository)
    {
        _interviewAiService = interviewAiService;
        _turnOrchestrator = turnOrchestrator;
        _interviewRepository = interviewRepository;
    }

    public async Task<InterviewStartResult> StartInterviewAsync(AppUser candidate, CancellationToken cancellationToken = default)
    {
        const int plannedCount = 12;

        _seedQuestions = await _interviewAiService.GenerateQuestionsAsync(
            candidate.AiProvider,
            candidate.JobRole,
            candidate.InterviewCategory,
            candidate.JobDescription,
            candidate.InterviewDifficulty,
            count: plannedCount,
            cancellationToken);

        _session = new InterviewSession
        {
            CandidateUserId = candidate.UserId,
            JobRole = candidate.JobRole,
            JobDescription = candidate.JobDescription,
            Category = candidate.InterviewCategory,
            Difficulty = candidate.InterviewDifficulty,
            PassingScore = candidate.PassingScore,
            AiProvider = candidate.AiProvider,
            StartedAtUtc = DateTime.UtcNow,
            Status = InterviewStatus.InProgress,
            PlannedQuestionCount = plannedCount,
            FlowState = InterviewFlowState.Intro,
        };

        _questions.Clear();
        _currentIndex = 0;
        _seedCursor = 0;

        var intro = await _interviewAiService.GenerateProfessionalIntroductionAsync(
            candidate.AiProvider,
            candidate.UserId,
            candidate.JobRole,
            candidate.InterviewCategory,
            candidate.InterviewDifficulty,
            cancellationToken);
        intro = NormalizeIntro(intro);

        AddTurn("AI Interviewer", "intro", intro);

        var firstQuestion = _seedQuestions.FirstOrDefault() ?? new InterviewQuestion
        {
            Prompt = "Please introduce yourself and summarize your most relevant experience for this role.",
            IdealAnswerHint = "Concise background, relevant achievements, role alignment.",
        };
        _questions.Add(firstQuestion);
        _seedCursor = _seedQuestions.Count > 0 ? 1 : 0;
        AddTurn("AI Interviewer", "question", firstQuestion.Prompt);
        _session.FlowState = InterviewFlowState.AskQuestion;

        return new InterviewStartResult
        {
            Session = _session,
            Questions = _questions.ToList(),
            IntroductionMessage = intro,
        };
    }

    public async Task<InterviewSubmitResult> SubmitAnswerAsync(string transcript, CancellationToken cancellationToken = default)
    {
        if (_session is null || _questions.Count == 0)
        {
            throw new InvalidOperationException("Interview has not started.");
        }

        if (_currentIndex >= _questions.Count)
        {
            throw new InvalidOperationException("Interview already completed.");
        }

        _session.FlowState = InterviewFlowState.CandidateAnswer;
        var answerText = transcript.Trim();
        AddTurn("Candidate", "answer", answerText);
        var isSkip = IsSkipOrUnknownResponse(answerText);

        var question = _questions[_currentIndex];
        double blendedScore;
        bool isCorrect;
        string encouragement;
        InterviewTurnDecision? turnDecision = null;

        if (isSkip)
        {
            blendedScore = 0;
            isCorrect = false;
            encouragement = "No problem. I will mark this one as skipped and move to the next question.";
        }
        else
        {
            turnDecision = await _turnOrchestrator.DecideNextTurnAsync(
                _session.AiProvider,
                _session,
                question,
                answerText,
                _currentIndex,
                _session.PlannedQuestionCount,
                cancellationToken);

            blendedScore = Math.Round(turnDecision.OverallScore, 2);
            isCorrect = blendedScore >= _session.PassingScore;
            encouragement = turnDecision.Acknowledgement;
        }
        _session.FlowState = InterviewFlowState.Encourage;
        AddTurn("AI Interviewer", "encouragement", encouragement);

        var result = new InterviewQuestionResult
        {
            QuestionId = question.Id,
            Prompt = question.Prompt,
            IdealAnswerHint = question.IdealAnswerHint,
            CandidateTranscript = answerText,
            IsCorrect = isCorrect,
            ScoreAwarded = Math.Clamp(blendedScore, 0, 100),
            TechnicalScore = turnDecision?.TechnicalScore ?? 0,
            CommunicationScore = turnDecision?.CommunicationScore ?? 0,
            DepthScore = turnDecision?.DepthScore ?? 0,
            RelevanceScore = turnDecision?.RelevanceScore ?? 0,
            ProblemSolvingScore = turnDecision?.ProblemSolvingScore ?? 0,
            Topic = turnDecision?.Topic ?? string.Empty,
            Strengths = turnDecision is null ? string.Empty : string.Join("; ", turnDecision.ObservedStrengths),
            Gaps = turnDecision is null ? string.Empty : string.Join("; ", turnDecision.ObservedGaps),
            Feedback = isSkip
                ? "Marked as skipped. Score: 0/100."
                : $"{encouragement} {turnDecision?.EvaluationSummary}".Trim(),
        };

        _session.QuestionResults.Add(result);
        _currentIndex++;

        var runningScore = _session.QuestionResults.Average(x => x.ScoreAwarded);
        InterviewQuestion? nextQuestion = null;
        var isAdaptiveFollowUp = false;

        if (_currentIndex < _session.PlannedQuestionCount)
        {
            _session.FlowState = InterviewFlowState.FollowUp;
            var currentWasFollowUp = IsFollowUpQuestion(question);
            if (!isSkip &&
                !currentWasFollowUp &&
                turnDecision is not null &&
                turnDecision.ShouldAskClarification &&
                !string.IsNullOrWhiteSpace(turnDecision.ClarificationPrompt))
            {
                nextQuestion = new InterviewQuestion
                {
                    Prompt = $"Follow-up: {turnDecision.ClarificationPrompt}",
                    IdealAnswerHint = EnsureHintMetadata(turnDecision.IdealAnswerHint, "followup", "medium"),
                };
                isAdaptiveFollowUp = true;
            }
            else
            {
                nextQuestion = GetNextSeedQuestion();
            }

            if (nextQuestion is null || string.IsNullOrWhiteSpace(nextQuestion.Prompt))
            {
                nextQuestion = GetNextSeedQuestion() ?? new InterviewQuestion
                {
                    Prompt = "Tell me about one technical challenge you solved in a project.",
                    IdealAnswerHint = "Section:project; Expected:medium; Real scenario, approach, decision, measurable impact.",
                };
            }

            _questions.Add(nextQuestion);
            AddTurn("AI Interviewer", isAdaptiveFollowUp ? "followup" : "question", nextQuestion.Prompt);
            _session.FlowState = InterviewFlowState.AskQuestion;
        }

        return new InterviewSubmitResult
        {
            Result = result,
            NextQuestion = nextQuestion,
            IsInterviewCompleted = nextQuestion is null,
            RunningScore = Math.Round(runningScore, 2),
            EncouragementMessage = encouragement,
            IsAdaptiveFollowUp = isAdaptiveFollowUp,
        };
    }

    public async Task<InterviewSubmitResult> ResubmitAnswerAsync(string questionId, string transcript, CancellationToken cancellationToken = default)
    {
        if (_session is null || _questions.Count == 0)
        {
            throw new InvalidOperationException("Interview has not started.");
        }

        var questionIndex = _questions.FindIndex(question => question.Id.Equals(questionId, StringComparison.Ordinal));
        if (questionIndex < 0)
        {
            throw new InvalidOperationException("Question is no longer available for review.");
        }

        var existingResultIndex = _session.QuestionResults.FindIndex(result => result.QuestionId.Equals(questionId, StringComparison.Ordinal));
        if (existingResultIndex < 0)
        {
            throw new InvalidOperationException("Only answered questions can be re-submitted.");
        }

        var question = _questions[questionIndex];
        var answerText = transcript.Trim();
        _session.FlowState = InterviewFlowState.CandidateAnswer;
        AddTurn("Candidate", "answer_revision", answerText);

        var isSkip = IsSkipOrUnknownResponse(answerText);
        double blendedScore;
        bool isCorrect;
        string encouragement;
        InterviewTurnDecision? turnDecision = null;

        if (isSkip)
        {
            blendedScore = 0;
            isCorrect = false;
            encouragement = "Updated. I will keep this one marked as skipped.";
        }
        else
        {
            turnDecision = await _turnOrchestrator.DecideNextTurnAsync(
                _session.AiProvider,
                _session,
                question,
                answerText,
                questionIndex,
                _session.PlannedQuestionCount,
                cancellationToken);

            blendedScore = Math.Round(turnDecision.OverallScore, 2);
            isCorrect = blendedScore >= _session.PassingScore;
            encouragement = "Updated answer saved.";
        }

        var result = new InterviewQuestionResult
        {
            QuestionId = question.Id,
            Prompt = question.Prompt,
            IdealAnswerHint = question.IdealAnswerHint,
            CandidateTranscript = answerText,
            IsCorrect = isCorrect,
            ScoreAwarded = Math.Clamp(blendedScore, 0, 100),
            TechnicalScore = turnDecision?.TechnicalScore ?? 0,
            CommunicationScore = turnDecision?.CommunicationScore ?? 0,
            DepthScore = turnDecision?.DepthScore ?? 0,
            RelevanceScore = turnDecision?.RelevanceScore ?? 0,
            ProblemSolvingScore = turnDecision?.ProblemSolvingScore ?? 0,
            Topic = turnDecision?.Topic ?? string.Empty,
            Strengths = turnDecision is null ? string.Empty : string.Join("; ", turnDecision.ObservedStrengths),
            Gaps = turnDecision is null ? string.Empty : string.Join("; ", turnDecision.ObservedGaps),
            Feedback = isSkip
                ? "Updated and marked as skipped. Score: 0/100."
                : $"{encouragement} {turnDecision?.EvaluationSummary}".Trim(),
        };

        _session.QuestionResults[existingResultIndex] = result;
        AddTurn("AI Interviewer", "revision_acknowledgement", encouragement);
        _session.FlowState = InterviewFlowState.AskQuestion;

        var runningScore = _session.QuestionResults.Count == 0
            ? 0
            : _session.QuestionResults.Average(x => x.ScoreAwarded);

        return new InterviewSubmitResult
        {
            Result = result,
            NextQuestion = GetCurrentQuestion(),
            IsInterviewCompleted = false,
            RunningScore = Math.Round(runningScore, 2),
            EncouragementMessage = encouragement,
            IsAdaptiveFollowUp = false,
        };
    }

    public async Task<InterviewSession> FinishInterviewAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null)
        {
            throw new InvalidOperationException("Interview has not started.");
        }

        _session.CompletedAtUtc = DateTime.UtcNow;
        _session.Status = InterviewStatus.Completed;
        _session.FlowState = InterviewFlowState.Completed;
        _session.OverallScore = Math.Round(
            _session.QuestionResults.Count == 0 ? 0 : _session.QuestionResults.Average(x => x.ScoreAwarded),
            2);
        _session.IsPassed = _session.OverallScore >= _session.PassingScore;

        try
        {
            _session.RoleFitScore = await _interviewAiService.CalculateRoleFitScoreAsync(
                _session.AiProvider,
                _session.JobRole,
                _session.JobDescription,
                _session.Difficulty,
                _session.QuestionResults,
                cancellationToken);
        }
        catch
        {
            _session.RoleFitScore = _session.OverallScore;
        }

        await _interviewRepository.SaveAsync(_session, cancellationToken);
        return _session;
    }

    public InterviewQuestion? GetCurrentQuestion()
    {
        if (_questions.Count == 0 || _currentIndex >= _questions.Count)
        {
            return null;
        }

        return _questions[_currentIndex];
    }

    public int GetCurrentQuestionIndex() => _currentIndex;

    public InterviewQuestion? GetQuestionById(string questionId)
    {
        return _questions.FirstOrDefault(question => question.Id.Equals(questionId, StringComparison.Ordinal));
    }

    public InterviewQuestionResult? GetQuestionResult(string questionId)
    {
        return _session?.QuestionResults.FirstOrDefault(result => result.QuestionId.Equals(questionId, StringComparison.Ordinal));
    }

    public int GetTotalQuestionCount() => _session?.PlannedQuestionCount ?? _questions.Count;

    public bool HasActiveInterview() => _session?.Status == InterviewStatus.InProgress;

    public void Reset()
    {
        _session = null;
        _questions.Clear();
        _seedQuestions = [];
        _currentIndex = 0;
        _seedCursor = 0;
    }

    private InterviewQuestion? GetNextSeedQuestion()
    {
        if (_seedQuestions.Count == 0)
        {
            return null;
        }

        while (_seedCursor < _seedQuestions.Count)
        {
            var next = _seedQuestions[_seedCursor++];
            if (!_questions.Any(question => question.Id == next.Id))
            {
                return next;
            }
        }

        return null;
    }

    private void AddTurn(string speaker, string turnType, string text)
    {
        if (_session is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _session.ConversationTurns.Add(new InterviewConversationTurn
        {
            SequenceNumber = _session.ConversationTurns.Count + 1,
            Speaker = speaker,
            TurnType = turnType,
            Text = text.Trim(),
            TimestampUtc = DateTime.UtcNow,
        });
    }

    private static double CalculateKeywordScore(string hint, string transcript)
    {
        var expected = ToKeywords(hint);
        if (expected.Count == 0)
        {
            return 0;
        }

        var actual = ToKeywords(transcript);
        var matched = expected.Count(x => actual.Contains(x));
        return Math.Round((double)matched / expected.Count * 100.0, 2);
    }

    private static HashSet<string> ToKeywords(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '-', '_', '\n', '\r', '\t', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string EnsureHintMetadata(string hint, string section, string expected)
    {
        if (section.Equals("followup", StringComparison.OrdinalIgnoreCase))
        {
            var followUpBody = string.IsNullOrWhiteSpace(hint)
                ? "Specific answer with context, action, technical detail, and outcome."
                : hint.Trim();

            return $"Section:followup; Expected:{expected}; {followUpBody}";
        }

        if (!string.IsNullOrWhiteSpace(hint) &&
            hint.Contains("Section:", StringComparison.OrdinalIgnoreCase) &&
            hint.Contains("Expected:", StringComparison.OrdinalIgnoreCase))
        {
            return hint;
        }

        var body = string.IsNullOrWhiteSpace(hint)
            ? "Specific answer with context, action, technical detail, and outcome."
            : hint.Trim();

        return $"Section:{section}; Expected:{expected}; {body}";
    }

    private static bool IsFollowUpQuestion(InterviewQuestion question)
    {
        return ExtractHintValue(question.IdealAnswerHint, "Section").Equals("followup", StringComparison.OrdinalIgnoreCase) ||
               question.Prompt.StartsWith("Follow-up:", StringComparison.OrdinalIgnoreCase);
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

    private static string NormalizeIntro(string intro)
    {
        var clean = string.Join(' ', (intro ?? string.Empty)
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(clean))
        {
            return "Welcome. I will ask one focused question at a time and listen for clear, concrete answers.";
        }

        const int maxLength = 150;
        if (clean.Length <= maxLength)
        {
            return clean;
        }

        var cut = clean.LastIndexOf(' ', maxLength);
        return $"{clean[..(cut > 0 ? cut : maxLength)].TrimEnd('.')}.";
    }

    private static bool IsSkipOrUnknownResponse(string answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return true;
        }

        var normalized = answerText.Trim().ToLowerInvariant();
        var skipPhrases = new[]
        {
            "skip",
            "i dont know",
            "i don't know",
            "dont know",
            "do not know",
            "no idea",
            "not sure",
            "can't answer",
            "cannot answer",
            "pass",
        };

        return skipPhrases.Any(phrase => normalized.Equals(phrase, StringComparison.OrdinalIgnoreCase) ||
                                         normalized.StartsWith(phrase + " ", StringComparison.OrdinalIgnoreCase));
    }
}
