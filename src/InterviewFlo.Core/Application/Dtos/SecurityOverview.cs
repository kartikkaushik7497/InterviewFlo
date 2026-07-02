namespace InterviewFlo.Core.Application.Dtos;

public sealed class SecurityOverview
{
    public bool HasOpenAiApiKey { get; init; }

    public string KeySource { get; init; } = "unset";

    public string PasswordPolicySummary { get; init; } = string.Empty;
}
