using InterviewFlo.Core.Domain.Entities;

namespace InterviewFlo.Core.Application.Dtos;

public sealed record AuthResult(
    bool IsSuccess,
    string Message,
    AppUser? User,
    bool RequiresPasswordReset);
