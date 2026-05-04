namespace MockUpAi.Core.Application.Dtos;

public sealed class ReportExportRequest
{
    public required CandidateDashboardQuery Query { get; init; }

    public required string OutputDirectory { get; init; }

    public required string Format { get; init; }
}
