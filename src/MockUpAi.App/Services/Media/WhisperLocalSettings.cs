namespace MockUpAi.App.Services.Media;

public sealed class WhisperLocalSettings
{
    public bool Enabled { get; set; } = true;

    public string Model { get; set; } = "base.en";

    public string? ModelPath { get; set; }

    public bool AutoDownload { get; set; } = true;

    public bool AccurateRetryEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 75;

    public int MaxThreads { get; set; } = 8;

    public int SilencePaddingMs { get; set; } = 220;

    public int MinimumSpeechMs { get; set; } = 350;
}
