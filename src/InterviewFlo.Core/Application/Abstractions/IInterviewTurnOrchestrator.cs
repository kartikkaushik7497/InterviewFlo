using InterviewFlo.Core.Application.Dtos;
using InterviewFlo.Core.Domain.Entities;
using InterviewFlo.Core.Domain.Enums;

namespace InterviewFlo.Core.Application.Abstractions;

public interface IInterviewTurnOrchestrator
{
    Task<InterviewTurnDecision> DecideNextTurnAsync(
        InterviewAiProvider provider,
        InterviewSession session,
        InterviewQuestion currentQuestion,
        string candidateAnswer,
        int currentQuestionIndex,
        int totalQuestionCount,
        CancellationToken cancellationToken = default);
}
