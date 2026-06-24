namespace MockUpAi.App.ViewModels.Candidate;

public sealed class InterviewChatMessage
{
    public string Sender { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public bool IsUser { get; init; }

    public string BubbleBackground { get; init; } = "#FFFFFF";

    public string BubbleBorder { get; init; } = "#D4DDE6";

    public string SenderColor { get; init; } = "#0F766E";

    public string BubbleAlignment { get; init; } = "Left";
}
