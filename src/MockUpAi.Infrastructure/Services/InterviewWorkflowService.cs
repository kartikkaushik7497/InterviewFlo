using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Infrastructure.Services;

internal sealed class InterviewWorkflowService : IInterviewWorkflowService
{
    private readonly IInterviewAiService _interviewAiService;
    private readonly IInterviewRepository _interviewRepository;

    private InterviewSession? _session;
    private readonly List<InterviewQuestion> _questions = [];
    private IReadOnlyList<InterviewQuestion> _seedQuestions = [];
    private int _currentIndex;

    public InterviewWorkflowService(
        IInterviewAiService interviewAiService,
        IInterviewRepository interviewRepository)
    {
        _interviewAiService = interviewAiService;
        _interviewRepository = interviewRepository;
    }

    public async Task<InterviewStartResult> StartInterviewAsync(AppUser candidate, CancellationToken cancellationToken = default)
    {
        const int plannedCount = 5;

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

        var intro = await _interviewAiService.GenerateProfessionalIntroductionAsync(
            candidate.AiProvider,
            candidate.UserId,
            candidate.JobRole,
            candidate.InterviewCategory,
            candidate.InterviewDifficulty,
            cancellationToken);

        AddTurn("AI Interviewer", "intro", intro);

        var firstQuestion = _seedQuestions.FirstOrDefault() ?? new InterviewQuestion
        {
            Prompt = "Please introduce yourself and summarize your most relevant experience for this role.",
            IdealAnswerHint = "Concise background, relevant achievements, role alignment.",
        };
        _questions.Add(firstQuestion);
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

        var question = _questions[_currentIndex];
        var evaluation = await _interviewAiService.EvaluateAnswerAsync(
            _session.AiProvider,
            _session.JobRole,
            _session.Difficulty,
            question,
            answerText,
            cancellationToken);

        var keywordScore = CalculateKeywordScore(question.IdealAnswerHint, answerText);
        var blendedScore = Math.Round((evaluation.Score * 0.75) + (keywordScore * 0.25), 2);
        var isCorrect = blendedScore >= _session.PassingScore;

        var encouragement = await _interviewAiService.GenerateEncouragementAsync(
            _session.AiProvider,
            _session.JobRole,
            _session.Category,
            _session.Difficulty,
            answerText,
            cancellationToken);
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
            Feedback = $"{encouragement} {evaluation.Feedback} Keyword match: {keywordScore:0.##}/100.",
        };

        _session.QuestionResults.Add(result);
        _currentIndex++;

        var runningScore = _session.QuestionResults.Average(x => x.ScoreAwarded);
        InterviewQuestion? nextQuestion = null;

        if (_currentIndex < _session.PlannedQuestionCount)
        {
            _session.FlowState = InterviewFlowState.FollowUp;
            nextQuestion = await _interviewAiService.GenerateFollowUpQuestionAsync(
                _session.AiProvider,
                _session.JobRole,
                _session.Category,
                _session.Difficulty,
                question,
                answerText,
                _session.ConversationTurns,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(nextQuestion.Prompt))
            {
                nextQuestion = _seedQuestions.ElementAtOrDefault(_currentIndex) ?? new InterviewQuestion
                {
                    Prompt = "Please expand that with a concrete example from a real project.",
                    IdealAnswerHint = "Real scenario, approach, decision, measurable impact.",
                };
            }

            _questions.Add(nextQuestion);
            AddTurn("AI Interviewer", "question", nextQuestion.Prompt);
            _session.FlowState = InterviewFlowState.AskQuestion;
        }

        return new InterviewSubmitResult
        {
            Result = result,
            NextQuestion = nextQuestion,
            IsInterviewCompleted = nextQuestion is null,
            RunningScore = Math.Round(runningScore, 2),
            EncouragementMessage = encouragement,
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

        _session.RoleFitScore = await _interviewAiService.CalculateRoleFitScoreAsync(
            _session.AiProvider,
            _session.JobRole,
            _session.JobDescription,
            _session.Difficulty,
            _session.QuestionResults,
            cancellationToken);

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

    public int GetTotalQuestionCount() => _session?.PlannedQuestionCount ?? _questions.Count;

    public bool HasActiveInterview() => _session?.Status == InterviewStatus.InProgress;

    public void Reset()
    {
        _session = null;
        _questions.Clear();
        _seedQuestions = [];
        _currentIndex = 0;
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
}
