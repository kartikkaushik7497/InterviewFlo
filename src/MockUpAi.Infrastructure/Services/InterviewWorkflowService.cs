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
    private IReadOnlyList<InterviewQuestion> _questions = [];
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
        _questions = await _interviewAiService.GenerateQuestionsAsync(
            candidate.JobRole,
            candidate.JobDescription,
            count: 5,
            cancellationToken);

        _session = new InterviewSession
        {
            CandidateUserId = candidate.UserId,
            JobRole = candidate.JobRole,
            JobDescription = candidate.JobDescription,
            StartedAtUtc = DateTime.UtcNow,
            Status = InterviewStatus.InProgress,
        };

        _currentIndex = 0;

        return new InterviewStartResult
        {
            Session = _session,
            Questions = _questions,
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

        var question = _questions[_currentIndex];
        var evaluation = await _interviewAiService.EvaluateAnswerAsync(
            _session.JobRole,
            question,
            transcript,
            cancellationToken);

        var result = new InterviewQuestionResult
        {
            QuestionId = question.Id,
            Prompt = question.Prompt,
            IdealAnswerHint = question.IdealAnswerHint,
            CandidateTranscript = transcript.Trim(),
            IsCorrect = evaluation.IsCorrect,
            ScoreAwarded = evaluation.Score,
            Feedback = evaluation.Feedback,
        };

        _session.QuestionResults.Add(result);
        _currentIndex++;

        var runningScore = _session.QuestionResults.Average(x => x.ScoreAwarded);
        var nextQuestion = _currentIndex < _questions.Count ? _questions[_currentIndex] : null;

        return new InterviewSubmitResult
        {
            Result = result,
            NextQuestion = nextQuestion,
            IsInterviewCompleted = nextQuestion is null,
            RunningScore = Math.Round(runningScore, 2),
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
        _session.OverallScore = Math.Round(
            _session.QuestionResults.Count == 0 ? 0 : _session.QuestionResults.Average(x => x.ScoreAwarded),
            2);

        _session.RoleFitScore = await _interviewAiService.CalculateRoleFitScoreAsync(
            _session.JobRole,
            _session.JobDescription,
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

    public int GetTotalQuestionCount() => _questions.Count;

    public bool HasActiveInterview() => _session?.Status == InterviewStatus.InProgress;

    public void Reset()
    {
        _session = null;
        _questions = [];
        _currentIndex = 0;
    }
}
