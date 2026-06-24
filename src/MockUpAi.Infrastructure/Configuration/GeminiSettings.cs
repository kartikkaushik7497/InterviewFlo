namespace MockUpAi.Infrastructure.Configuration;

public sealed class GeminiSettings
{
    public bool Enabled { get; set; } = false;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-1.5-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
