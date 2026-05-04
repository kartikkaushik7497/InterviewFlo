using MockUpAi.Core.Domain.Entities;

namespace MockUpAi.Core.Application.Dtos;

public sealed record AuthResult(
    bool IsSuccess,
    string Message,
    AppUser? User,
    bool RequiresPasswordReset);
