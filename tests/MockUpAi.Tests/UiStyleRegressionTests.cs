using System.Xml.Linq;

namespace MockUpAi.Tests;

public sealed class UiStyleRegressionTests
{
    [Fact]
    public void SharedButtonStyles_DefineVisibleDefaultHoverPressedAndDisabledStates()
    {
        var appMarkup = File.ReadAllText(FindRepoFile("src", "MockUpAi.App", "App.axaml"));

        var requiredSelectors = new[]
        {
            "Button:pointerover",
            "Button:pressed",
            "Button:disabled",
            "Button /template/ ContentPresenter",
            "Button:disabled /template/ ContentPresenter",
            "Button.LightPrimary:pointerover",
            "Button.LightPrimary:pressed",
            "Button.LightPrimary:disabled",
            "Button.LightNeutral:pointerover",
            "Button.LightNeutral:pressed",
            "Button.LightNeutral:disabled",
            "Button.LightDanger:pointerover",
            "Button.LightDanger:pressed",
            "Button.LightDanger:disabled",
            "Button.Primary:disabled",
            "Button.Danger:disabled"
        };

        foreach (var selector in requiredSelectors)
        {
            Assert.Contains($"Selector=\"{selector}\"", appMarkup);
        }

        Assert.Contains("Opacity\" Value=\"1\"", appMarkup);
        Assert.Contains("Property=\"Template\"", GetStyleBlock(appMarkup, "Button"));
        Assert.Contains("Background=\"{TemplateBinding Background}\"", GetStyleBlock(appMarkup, "Button"));
        Assert.Contains("BorderBrush=\"{TemplateBinding BorderBrush}\"", GetStyleBlock(appMarkup, "Button"));
        Assert.Contains("Foreground=\"{TemplateBinding Foreground}\"", GetStyleBlock(appMarkup, "Button"));
        Assert.DoesNotContain("PART_ContentPresenter", appMarkup);
    }

    [Fact]
    public void SharedButtonStyles_HoverStatesUseTintedVariantsInsteadOfWhite()
    {
        var appMarkup = File.ReadAllText(FindRepoFile("src", "MockUpAi.App", "App.axaml"));
        var loginMarkup = File.ReadAllText(FindRepoFile("src", "MockUpAi.App", "Views", "Auth", "LoginView.axaml"));

        Assert.Contains("Background\" Value=\"#3B475A\"", GetStyleBlock(appMarkup, "Button:pointerover"));
        Assert.Contains("Background\" Value=\"#334155\"", GetStyleBlock(appMarkup, "Button.IconButton:pointerover"));
        Assert.Contains("Background\" Value=\"#3B82F6\"", GetStyleBlock(appMarkup, "Button.LightPrimary:pointerover"));
        Assert.Contains("Background\" Value=\"#E2E8F0\"", GetStyleBlock(appMarkup, "Button.LightNeutral:pointerover"));
        Assert.Contains("Background\" Value=\"#B91C1C\"", GetStyleBlock(appMarkup, "Button.LightDanger:pointerover"));
        Assert.Contains("Background\" Value=\"#FB923C\"", GetStyleBlock(appMarkup, "Button.Primary:pointerover"));
        Assert.Contains("Background\" Value=\"#991B1B\"", GetStyleBlock(appMarkup, "Button.Danger:pointerover"));
        Assert.Contains("Background\" Value=\"#FB923C\"", GetStyleBlock(loginMarkup, "Button.LoginCta:pointerover"));
    }

    [Fact]
    public void CandidateScreens_UseExplicitLightButtonClasses()
    {
        var candidateViewsRoot = Path.Combine(FindRepoRoot(), "src", "MockUpAi.App", "Views", "Candidate");
        var files = Directory.GetFiles(candidateViewsRoot, "*.axaml", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            var document = XDocument.Load(file);
            var buttons = document.Descendants().Where(element => element.Name.LocalName == "Button");

            foreach (var button in buttons)
            {
                var classes = button.Attribute("Classes")?.Value ?? string.Empty;

                Assert.True(
                    classes.Contains("Light", StringComparison.Ordinal) ||
                    classes.Contains("IconButton", StringComparison.Ordinal),
                    $"{Path.GetFileName(file)} has a candidate-facing button without a light-theme button class.");
            }
        }
    }

    [Fact]
    public void LoginCallToAction_KeepsTextVisibleAcrossStates()
    {
        var loginPath = FindRepoFile("src", "MockUpAi.App", "Views", "Auth", "LoginView.axaml");
        var markup = File.ReadAllText(loginPath);
        var document = XDocument.Load(loginPath);
        var loginButton = document
            .Descendants()
            .First(element =>
                element.Name.LocalName == "Button" &&
                (element.Attribute("Classes")?.Value ?? string.Empty).Contains("LoginCta", StringComparison.Ordinal));

        var visibleLabel = loginButton
            .Descendants()
            .First(element =>
                element.Name.LocalName == "TextBlock" &&
                element.Attribute("Text")?.Value == "Login");

        Assert.Contains("Button.LoginCta:disabled", markup);
        Assert.Contains("Button.LoginCta:pointerover", markup);
        Assert.Contains("Button.LoginCta:pressed", markup);
        Assert.Contains("AncestorType=Button", visibleLabel.Attribute("Foreground")?.Value);
    }

    [Fact]
    public void DarkThemeInputsAndCheckboxes_DoNotRenderBlackOrInvisibleText()
    {
        var appMarkup = File.ReadAllText(FindRepoFile("src", "MockUpAi.App", "App.axaml"));

        Assert.Contains("PlaceholderForeground\" Value=\"#B6C2D3\"", GetStyleBlock(appMarkup, "TextBox"));
        Assert.Contains("PlaceholderForeground\" Value=\"#334155\"", GetStyleBlock(appMarkup, "TextBox:focus, TextBox:focus-within"));
        Assert.Contains("PlaceholderForeground\" Value=\"#475569\"", GetStyleBlock(appMarkup, "TextBox.LightInput"));
        Assert.Contains("Background\" Value=\"#2A2F38\"", GetStyleBlock(appMarkup, "TextBox /template/ Border#PART_BorderElement, ComboBox /template/ Border#PART_BorderElement"));
        Assert.Contains("CheckBox /template/ ContentPresenter", appMarkup);
        Assert.Contains("Foreground\" Value=\"{Binding Foreground, RelativeSource={RelativeSource TemplatedParent}}\"", GetStyleBlock(appMarkup, "CheckBox /template/ ContentPresenter"));
    }

    [Fact]
    public void AdminCheckboxLabels_UseExplicitReadableDarkThemeText()
    {
        var adminPath = FindRepoFile("src", "MockUpAi.App", "Views", "Admin", "AdminDashboardView.axaml");
        var markup = File.ReadAllText(adminPath);
        var document = XDocument.Load(adminPath);
        var expectedLabels = new[] { "Account Active", "Desc", "Live updates" };

        foreach (var label in expectedLabels)
        {
            Assert.DoesNotContain($"Content=\"{label}\"", markup);

            var labelBlock = document
                .Descendants()
                .FirstOrDefault(element =>
                    element.Name.LocalName == "TextBlock" &&
                    element.Attribute("Text")?.Value == label);

            Assert.NotNull(labelBlock);
            Assert.Equal("#CBD5E1", labelBlock.Attribute("Foreground")?.Value);
        }
    }

    [Fact]
    public void InterviewScreen_ClipsScrollableContentAwayFromFixedFooter()
    {
        var markup = File.ReadAllText(FindRepoFile(
            "src",
            "MockUpAi.App",
            "Views",
            "Candidate",
            "CandidateInterviewView.axaml"));

        Assert.Contains("RowDefinitions=\"Auto,*,Auto\" RowSpacing=\"10\" Margin=\"12\" ClipToBounds=\"True\"", markup);
        Assert.Contains("ColumnDefinitions=\"*,400\"", markup);
        Assert.Contains("Grid.Column=\"1\" RowDefinitions=\"Auto,*\" RowSpacing=\"10\" ClipToBounds=\"True\"", markup);
        Assert.Contains("Button.ProgressTrackButton", markup);
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        return Path.Combine(FindRepoRoot(), Path.Combine(pathParts));
    }

    private static string GetStyleBlock(string markup, string selector)
    {
        var selectorText = $"Selector=\"{selector}\"";
        var start = markup.IndexOf(selectorText, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find style selector {selector}.");

        var end = markup.IndexOf("</Style>", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find closing style tag for {selector}.");

        return markup[start..end];
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MockUpAi.sln")) &&
                File.Exists(Path.Combine(current.FullName, "src", "MockUpAi.App", "App.axaml")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the InterviewFlo repository root.");
    }
}
