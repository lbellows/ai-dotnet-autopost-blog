using BlogGenerator.Core.Configuration;

namespace BlogGenerator.Tests;

public class GenerationSettingsTests
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
        FoundryModels = ["gpt-5.4-mini", "gpt-5-mini"],
        FoundryDefaultModel = "gpt-5.4-mini",
        FoundryMaxTokens = 4096,
    };

    [Fact]
    public void NormalizeDedupesDomainsAndModels()
    {
        var settings = CreateSettings();
        settings.AllowedDomains.Add("learn.microsoft.com");
        settings.AllowedDomains.Add(" Learn.Microsoft.com ");
        settings.BlockedDomains.Add(" example.com ");
        settings.BlockedDomains.Add("EXAMPLE.COM");
        settings.FoundryModels.Add("gpt-5-mini");
        settings.FoundryModels.Add(" gpt-5-mini ");

        settings.Normalize();

        Assert.Equal(
            settings.AllowedDomains.Count,
            settings.AllowedDomains.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("learn.microsoft.com", settings.AllowedDomains);
        Assert.Single(settings.BlockedDomains);
        Assert.Equal("example.com", settings.BlockedDomains[0]);
        Assert.Equal(
            settings.FoundryModels.Count,
            settings.FoundryModels.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ValidateRejectsMissingConfiguredValues()
    {
        var settings = new GenerationSettings();
        var ex = Assert.Throws<InvalidOperationException>(settings.Validate);
        Assert.Contains("TopicHint", ex.Message);
    }

    // A feed row with half its fields filled in is a config typo. Dropping the row beats failing
    // the whole run, and the model types source names back, so they are normalized to lower case.
    [Fact]
    public void NormalizeDropsIncompleteFeedsAndLowercasesNames()
    {
        var settings = CreateSettings();
        settings.ResearchFeeds =
        [
            new FeedSource { Name = " DotNet ", Url = " https://devblogs.microsoft.com/dotnet/feed/ " },
            new FeedSource { Name = "no-url", Url = "  " },
            new FeedSource { Name = "", Url = "https://example.com/feed" },
            new FeedSource { Name = "duplicate", Url = "https://devblogs.microsoft.com/dotnet/feed/" },
        ];

        settings.Normalize();

        var feed = Assert.Single(settings.ResearchFeeds);
        Assert.Equal("dotnet", feed.Name);
        Assert.Equal("https://devblogs.microsoft.com/dotnet/feed/", feed.Url);
    }

    [Fact]
    public void ValidateRejectsAFeedUrlThatIsNotAbsoluteHttp()
    {
        var settings = ValidSettings();
        settings.ResearchFeeds = [new FeedSource { Name = "broken", Url = "devblogs.microsoft.com/feed" }];

        var ex = Assert.Throws<InvalidOperationException>(settings.Validate);

        Assert.Contains("broken", ex.Message);
    }

    [Fact]
    public void ValidateRequiresTheFeedCapsOnlyWhenFeedsAreConfigured()
    {
        var withoutFeeds = ValidSettings();
        withoutFeeds.ResearchFeedItemsPerSource = 0;
        withoutFeeds.ResearchPageMaxChars = 0;
        withoutFeeds.Validate();

        var withFeeds = ValidSettings();
        withFeeds.ResearchFeedItemsPerSource = 0;
        withFeeds.ResearchFeeds = [new FeedSource { Name = "dotnet", Url = "https://devblogs.microsoft.com/dotnet/feed/" }];

        Assert.Contains("ResearchFeedItemsPerSource", Assert.Throws<InvalidOperationException>(withFeeds.Validate).Message);
    }

    private static GenerationSettings ValidSettings()
    {
        var settings = CreateSettings();
        settings.VeniceBrainModel = "grok-4-6";
        settings.VeniceResearchMaxTokens = 6000;
        settings.VeniceMaxTokens = 8192;
        settings.LocalMaxTokens = 8192;
        settings.LocalTimeoutMinutes = 30;
        settings.LocalResearchMaxTokens = 4096;
        settings.LocalResearchMaxRounds = 10;
        settings.ResearchFeedItemsPerSource = 15;
        settings.ResearchPageMaxChars = 12000;
        settings.CodeSampleMinLines = 15;
        settings.CodeSampleMaxLines = 30;
        return settings;
    }
}
