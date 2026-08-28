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
}
