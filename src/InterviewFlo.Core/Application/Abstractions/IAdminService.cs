using InterviewFlo.Core.Application.Dtos;
using InterviewFlo.Core.Common;

namespace InterviewFlo.Core.Application.Abstractions;

public interface IAdminService
{
    Task<OperationResult> CreateCandidateAsync(CandidateCreateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateCandidateAsync(CandidateUpdateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> SetCandidateLifecycleAsync(CandidateLifecycleUpdateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> ResetCandidatePasswordAsync(CandidatePasswordResetRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteCandidateAsync(string candidateId, CancellationToken cancellationToken = default);

    Task<OperationResult> RotateAdminPasswordAsync(AdminPasswordRotateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> SaveOpenAiApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    Task<SecurityOverview> GetSecurityOverviewAsync(CancellationToken cancellationToken = default);

    Task<string> ExportDashboardAsync(ReportExportRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandidateDashboardRow>> GetDashboardRowsAsync(CandidateDashboardQuery query, CancellationToken cancellationToken = default);
}
