namespace InterviewFlo.Core.Domain.Entities;

public sealed class InterviewConversationTurn
{
    public int SequenceNumber { get; set; }

    public string Speaker { get; set; } = string.Empty;

    public string TurnType { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
