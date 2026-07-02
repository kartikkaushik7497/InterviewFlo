namespace InterviewFlo.Core.Application.Dtos;

public sealed class CandidateDashboardQuery
{
    public string SearchText { get; set; } = string.Empty;

    public string RoleFilter { get; set; } = string.Empty;

    public bool? ActiveOnly { get; set; }

    public string SortBy { get; set; } = "completedAt";

    public bool SortDescending { get; set; } = true;
}
