using MockUpAi.Core.Application.Dtos;
using MockUpAi.Core.Domain.Entities;
using MockUpAi.Core.Domain.Enums;

namespace MockUpAi.Core.Application.Abstractions;

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
