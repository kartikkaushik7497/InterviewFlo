namespace MockUpAi.Core.Domain.Entities;

public sealed class InterviewQuestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Prompt { get; set; } = string.Empty;

    public string IdealAnswerHint { get; set; } = string.Empty;
}
