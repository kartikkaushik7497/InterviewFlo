using MockUpAi.Core.Application.Dtos;

namespace MockUpAi.Core.Application.Abstractions;

public interface IReportExportService
{
    Task<string> ExportAsync(IReadOnlyList<CandidateDashboardRow> rows, ReportExportRequest request, CancellationToken cancellationToken = default);
}
