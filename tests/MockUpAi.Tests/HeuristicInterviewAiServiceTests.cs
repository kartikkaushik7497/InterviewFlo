using MockUpAi.Core.Domain.Enums;
using MockUpAi.Infrastructure.Services;

namespace MockUpAi.Tests;

public sealed class HeuristicInterviewAiServiceTests
{
    [Fact]
    public async Task GenerateQuestionsAsync_UsesRoleSpecificPoolForQaRole()
    {
        var service = new HeuristicInterviewAiService();

        var questions = await service.GenerateQuestionsAsync(
            InterviewAiProvider.Heuristic,
            "Software QA Engineer",
            InterviewCategory.Technical,
            "Manual testing, automation, Selenium, API testing, defect reporting",
            InterviewDifficulty.Experienced,
            count: 12);

        Assert.Equal(12, questions.Count);
        Assert.Contains(questions, question =>
            question.Prompt.Contains("test", StringComparison.OrdinalIgnoreCase) ||
            question.Prompt.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
            question.Prompt.Contains("automation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerateProfessionalIntroductionAsync_ReturnsConciseOneLineIntro()
    {
        var service = new HeuristicInterviewAiService();

        var intro = await service.GenerateProfessionalIntroductionAsync(
            InterviewAiProvider.Heuristic,
            "Kartik",
            "Backend Developer",
            InterviewCategory.Technical,
            InterviewDifficulty.Fresher);

        Assert.Contains("Kartik", intro);
        Assert.DoesNotContain(Environment.NewLine, intro);
        Assert.True(intro.Length < 180);
    }
}
