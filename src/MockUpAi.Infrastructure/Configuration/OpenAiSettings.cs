namespace MockUpAi.Infrastructure.Configuration;

public sealed class OpenAiSettings
{
    public bool Enabled { get; set; } = false;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4.1-mini";

    public string TranscriptionModel { get; set; } = "gpt-4o-mini-transcribe";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}
