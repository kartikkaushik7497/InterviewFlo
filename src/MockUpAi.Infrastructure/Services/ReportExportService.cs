using System.Globalization;
using System.Text;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Application.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MockUpAi.Infrastructure.Services;

internal sealed class ReportExportService : IReportExportService
{
    public async Task<string> ExportAsync(IReadOnlyList<CandidateDashboardRow> rows, ReportExportRequest request, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(request.OutputDirectory);

        var extension = request.Format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "csv";
        var filePath = Path.Combine(request.OutputDirectory, $"InterviewFlo_Report_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");

        if (extension == "csv")
        {
            await ExportCsvAsync(rows, filePath, cancellationToken);
            return filePath;
        }

        ExportPdf(rows, filePath);
        return filePath;
    }

    private static async Task ExportCsvAsync(IReadOnlyList<CandidateDashboardRow> rows, string filePath, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CandidateId,JobRole,Status,InterviewScore,RoleFitScore,TechnicalScore,CommunicationScore,DepthScore,RelevanceScore,QuestionsAnswered,IsActive,IsExpired,ExpiresAtUtc,CompletedAtUtc");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(row.CandidateId),
                Csv(row.JobRole),
                Csv(row.InterviewStatus),
                row.LatestInterviewScore.ToString("0.00", CultureInfo.InvariantCulture),
                row.RoleFitScore.ToString("0.00", CultureInfo.InvariantCulture),
                row.TechnicalScore.ToString("0.00", CultureInfo.InvariantCulture),
                row.CommunicationScore.ToString("0.00", CultureInfo.InvariantCulture),
                row.DepthScore.ToString("0.00", CultureInfo.InvariantCulture),
                row.RelevanceScore.ToString("0.00", CultureInfo.InvariantCulture),
                row.QuestionsAnswered.ToString(CultureInfo.InvariantCulture),
                row.IsActive,
                row.IsExpired,
                Csv(row.ExpiresAtUtc?.ToString("O") ?? string.Empty),
                Csv(row.CompletedAtUtc?.ToString("O") ?? string.Empty)));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
    }

    private static void ExportPdf(IReadOnlyList<CandidateDashboardRow> rows, string filePath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().Text("InterviewFlo Candidate Interview Report").Bold().FontSize(18).FontColor(Colors.Blue.Darken2);
                    column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(12).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Candidate").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Role").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Status").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Score").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Fit").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Tech").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Comm").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Questions").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Active").Bold();
                    });

                    foreach (var row in rows)
                    {
                        DataCell(table, row.CandidateId);
                        DataCell(table, row.JobRole);
                        DataCell(table, row.InterviewStatus);
                        DataCell(table, row.LatestInterviewScore.ToString("0.0", CultureInfo.InvariantCulture));
                        DataCell(table, row.RoleFitScore.ToString("0.0", CultureInfo.InvariantCulture));
                        DataCell(table, row.TechnicalScore.ToString("0.0", CultureInfo.InvariantCulture));
                        DataCell(table, row.CommunicationScore.ToString("0.0", CultureInfo.InvariantCulture));
                        DataCell(table, row.QuestionsAnswered.ToString(CultureInfo.InvariantCulture));
                        DataCell(table, row.IsActive ? "Yes" : "No");
                    }
                });

                page.Footer().AlignRight().Text("Confidential - InterviewFlo").FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(filePath);
    }

    private static void DataCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text);
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
