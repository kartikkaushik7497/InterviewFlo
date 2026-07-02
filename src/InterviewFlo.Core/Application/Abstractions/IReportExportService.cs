using InterviewFlo.Core.Application.Dtos;

namespace InterviewFlo.Core.Application.Abstractions;

public interface IReportExportService
{
    Task<string> ExportAsync(IReadOnlyList<CandidateDashboardRow> rows, ReportExportRequest request, CancellationToken cancellationToken = default);
}
