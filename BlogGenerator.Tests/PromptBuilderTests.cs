using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;

namespace BlogGenerator.Tests;

public class PromptBuilderTests
{
    private static GenerationSettings CreateSettings() => new()
    {
        TopicHint = "Artificial Intelligence news for software engineers shipping on .NET and Azure.",
        PostWordsMin = 200,
        PostWordsMax = 1000,
        MaxSearches = 7,
        RecentWindowDays = 2,
        DefaultAuthor = "the.serf",
        AnthropicModel = "claude-sonnet-4-6",
        AnthropicMaxTokens = 4096,
        AnthropicTemperature = 0.9,
        FoundryModels = ["gpt-4.1-mini", "gpt-5.2-chat"],
        FoundryDefaultModel = "gpt-5.2-chat",
        FoundryMaxTokens = 4096,
    };

    [Fact]
    public void BuildProducesSundaySynopsisMode()
    {
        var settings = CreateSettings();
        var sunday = new DateOnly(2025, 6, 1); // Sunday
        var ctx = PromptBuilder.Build(settings, today: sunday);
        Assert.Contains("synopsis day", ctx.ModeInstructions);
    }

    [Fact]
    public void BuildProducesWeekdayDeepDiveMode()
    {
        var settings = CreateSettings();
        var monday = new DateOnly(2025, 6, 2); // Monday
        var ctx = PromptBuilder.Build(settings, today: monday);
        Assert.Contains("laser-focused", ctx.ModeInstructions);
    }

    [Fact]
    public void SystemPromptContainsWordLimits()
    {
        var settings = CreateSettings();
        settings.PostWordsMin = 300;
        settings.PostWordsMax = 800;
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        Assert.Contains("300-800", ctx.SystemPrompt);
    }

    [Fact]
    public void UserPromptContainsTopicHint()
    {
        var settings = CreateSettings();
        settings.TopicHint = "Custom topic hint";
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        Assert.Contains("Custom topic hint", ctx.UserPrompt);
    }

    [Fact]
    public void PrimaryLinkLineIncludedWhenSet()
    {
        var settings = CreateSettings();
        settings.TopicUrl = "https://example.com/article";
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        Assert.Contains("https://example.com/article", ctx.PrimaryLinkLine);
        Assert.True(ctx.UserInstructionItems.Count >= 5); // extra instruction added
    }

    [Fact]
    public void ImgflipGuidanceIncludedWhenEnabled()
    {
        var settings = CreateSettings();
        settings.ImgflipMemeEnabled = true;
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        Assert.Contains("meme", ctx.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecentStartDateIsCorrectDaysBack()
    {
        var settings = CreateSettings();
        settings.RecentWindowDays = 3;
        var today = new DateOnly(2025, 6, 10);
        var ctx = PromptBuilder.Build(settings, today: today);
        Assert.Equal(new DateOnly(2025, 6, 7), ctx.RecentStartDate);
    }

    [Fact]
    public void UserPromptIncludesDomainPreferencesWhenConfigured()
    {
        var settings = CreateSettings();
        settings.AllowedDomains.Add("learn.microsoft.com");
        settings.BlockedDomains.Add("example.com");

        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));

        Assert.Contains("learn.microsoft.com", ctx.UserPrompt);
        Assert.Contains("example.com", ctx.UserPrompt);
    }
}
