using MockUpAi.App.Models;
using MockUpAi.Core.Application.Abstractions;

namespace MockUpAi.App.Services.Media;

public interface IMediaPermissionService
{
    Task<PermissionCheckResult> CheckAllAsync(CancellationToken cancellationToken = default);
}
