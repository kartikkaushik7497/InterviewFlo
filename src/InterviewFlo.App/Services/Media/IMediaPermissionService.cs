using InterviewFlo.App.Models;
using InterviewFlo.Core.Application.Abstractions;

namespace InterviewFlo.App.Services.Media;

public interface IMediaPermissionService
{
    Task<PermissionCheckResult> CheckAllAsync(CancellationToken cancellationToken = default);
}
