using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Core.Application.Abstractions;

public interface IInterviewWorkflowService
{
    Task<InterviewStartResult> StartInterviewAsync(AppUser candidate, CancellationToken cancellationToken = default);

    Task<InterviewSubmitResult> SubmitAnswerAsync(string transcript, CancellationToken cancellationToken = default);

    Task<InterviewSubmitResult> ResubmitAnswerAsync(string questionId, string transcript, CancellationToken cancellationToken = default);

    Task<InterviewSession> FinishInterviewAsync(CancellationToken cancellationToken = default);

    InterviewQuestion? GetCurrentQuestion();

    InterviewQuestion? GetQuestionById(string questionId);

    InterviewQuestionResult? GetQuestionResult(string questionId);

    int GetCurrentQuestionIndex();

    int GetTotalQuestionCount();

    bool HasActiveInterview();

    void Reset();
}
