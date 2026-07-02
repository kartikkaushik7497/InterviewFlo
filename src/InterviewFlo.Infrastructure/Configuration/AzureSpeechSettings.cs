namespace InterviewFlo.Infrastructure.Configuration;

public sealed class AzureSpeechSettings
{
    public bool Enabled { get; set; } = false;

    public string ApiKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Language { get; set; } = "en-US";
}
