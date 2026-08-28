using BlogGenerator.Core.Configuration;
using Microsoft.Extensions.Configuration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Providers.Venice;

namespace BlogGenerator.Tests;

public class VeniceProviderTests
{
    private static GenerationSettings CreateSettings() => new()
    {
        TopicHint = "Artificial Intelligence news for software engineers shipping on .NET and Azure.",
        PostWordsMin = 200,
        PostWordsMax = 1000,
        MaxSearches = 7,
        RecentWindowDays = 3,
        DefaultAuthor = "the.serf",
        AnthropicModel = "claude-sonnet-4-6",
        AnthropicMaxTokens = 4096,
        FoundryDefaultModel = "gpt-5.4-mini",
        FoundryMaxTokens = 4096,
        AllowedDomains = ["devblogs.microsoft.com", "github.blog"],
        VeniceBrainModel = "grok-4-6",
        VeniceBrainFallbackModels = ["claude-sonnet-5"],
        VeniceWriterModel = "claude-sonnet-5",
        VeniceResearchMaxTokens = 6000,
        VeniceMaxTokens = 8192,
    };

    [Fact]
    public void BuildModelCandidatesPutsPrimaryFirstAndDedupes()
    {
        var candidates = VeniceProvider.BuildModelCandidates(
            " grok-4-6 ", ["claude-sonnet-5", "GROK-4-6", "  ", "zai-org-glm-5-2"]);

        Assert.Equal(["grok-4-6", "claude-sonnet-5", "zai-org-glm-5-2"], candidates);
    }

    [Fact]
    public void BuildModelCandidatesFallsBackWhenPrimaryEmpty()
    {
        var candidates = VeniceProvider.BuildModelCandidates("", ["claude-sonnet-5"]);

        Assert.Equal(["claude-sonnet-5"], candidates);
    }

    [Theory]
    [InlineData("Shipped on Tuesday.^4^", "Shipped on Tuesday.")]
    [InlineData("Confirmed by three sources.^1,5,8^", "Confirmed by three sources.")]
    [InlineData("Spaced marker ^ 2 , 3 ^ removed.", "Spaced marker removed.")]
    public void CleanModelTextStripsVeniceCitationMarkers(string input, string expected)
    {
        Assert.Equal(expected, VeniceProvider.CleanModelText(input));
    }

    [Fact]
    public void CleanModelTextPullsPunctuationBackAfterStrippingMarker()
    {
        Assert.Equal(
            "Azure shipped it, then GitHub followed.",
            VeniceProvider.CleanModelText("Azure shipped it^2^, then GitHub followed^3^."));
    }

    [Fact]
    public void CleanModelTextPreservesSpaceBeforeDotNet()
    {
        // Regression: a punctuation-tidying pass used to eat the space in ".NET", publishing
        // "most.NET developers". Marker removal already absorbs its own leading whitespace.
        const string text = "Most .NET developers on Azure ship .NET 10 apps, e.g. ASP.NET Core.";

        Assert.Equal(text, VeniceProvider.CleanModelText(text));
    }

    [Fact]
    public void CleanModelTextStripsSurvivingThinkingBlock()
    {
        var cleaned = VeniceProvider.CleanModelText("<think>\nlet me plan\n</think>\n# Real Title\n\nBody.");

        Assert.DoesNotContain("let me plan", cleaned);
        Assert.StartsWith("# Real Title", cleaned);
    }

    [Fact]
    public void CleanModelTextLeavesOrdinaryMarkdownAlone()
    {
        const string markdown = "# Title\n\n- item\n\n`var x = 1;`\n\nhttps://example.com/a";

        Assert.Equal(markdown, VeniceProvider.CleanModelText(markdown));
    }

    [Fact]
    public void ParseCompletionReadsContentAndCitations()
    {
        const string body = """
            {
              "model": "grok-4-6",
              "choices": [{ "message": { "role": "assistant", "content": "brief text" } }],
              "venice_parameters": {
                "web_search_citations": [
                  { "url": "https://devblogs.microsoft.com/dotnet/x/", "title": ".NET X", "date": "2026-08-26",
                    "content": "Today we <strong>announce</strong>\nthe release." },
                  { "url": "", "title": "dropped", "date": "", "content": "no url" }
                ]
              }
            }
            """;

        var completion = VeniceProvider.ParseCompletion(body, requestedModel: "requested");

        Assert.Equal("brief text", completion.Content);
        Assert.Equal("grok-4-6", completion.Model);
        var citation = Assert.Single(completion.Citations);
        Assert.Equal("https://devblogs.microsoft.com/dotnet/x/", citation.Url);
        Assert.Equal("2026-08-26", citation.Date);
        Assert.Equal("Today we announce the release.", citation.Snippet);
    }

    [Fact]
    public void ParseCompletionFallsBackToRequestedModelWhenResponseOmitsIt()
    {
        const string body = """{ "choices": [{ "message": { "content": "text" } }] }""";

        var completion = VeniceProvider.ParseCompletion(body, requestedModel: "claude-sonnet-5");

        Assert.Equal("claude-sonnet-5", completion.Model);
        Assert.Empty(completion.Citations);
    }

    [Fact]
    public void BuildDossierListsNotesAndVerifiedUrls()
    {
        var dossier = VeniceProvider.BuildDossier(
            ["### Research pass 1\n\nSomething shipped."],
            [new VeniceCitation("https://github.blog/y/", "GitHub Y", "2026-08-25", "snippet")]);

        Assert.Contains("Something shipped.", dossier);
        Assert.Contains("https://github.blog/y/", dossier);
        Assert.Contains("Use these URLs verbatim", dossier);
    }

    [Fact]
    public void BuildDossierOmitsUrlSectionWhenNoCitations()
    {
        var dossier = VeniceProvider.BuildDossier(["note"], []);

        Assert.DoesNotContain("Verified source URLs", dossier);
    }

    [Fact]
    public void ResearchAnglesCoverDistinctSourceTypes()
    {
        var settings = CreateSettings();

        var angles = PromptBuilder.ResearchAngles(
            settings, new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 24));

        Assert.Equal(4, angles.Count);
        Assert.Equal(angles.Count, angles.Distinct().Count());
        Assert.All(angles, angle => Assert.Contains("2026-08-24", angle));
        Assert.Contains(angles, angle => angle.Contains("devblogs.microsoft.com"));
    }

    [Fact]
    public void ResearchAnglesLeadWithTopicUrlWhenSet()
    {
        var settings = CreateSettings();
        settings.TopicUrl = "https://devblogs.microsoft.com/dotnet/anchor/";

        var angles = PromptBuilder.ResearchAngles(
            settings, new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 24));

        Assert.Contains("https://devblogs.microsoft.com/dotnet/anchor/", angles[0]);
    }

    [Fact]
    public void ResearchSystemPromptDemandsInWindowSeparation()
    {
        var prompt = PromptBuilder.ResearchSystemPrompt(
            CreateSettings(), new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 24));

        Assert.Contains("In-window findings", prompt);
        Assert.Contains("never invent a URL", prompt);
        Assert.Contains("2026-08-24", prompt);
    }

    [Fact]
    public void WriterSystemPromptKeepsHouseStyleButRemovesSearch()
    {
        var settings = CreateSettings();
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2026, 8, 27), rng: new Random(1));

        var prompt = PromptBuilder.WriterSystemPrompt(ctx, settings);

        Assert.Contains("A single H1 title on the first line", prompt);
        Assert.Contains("Further reading", prompt);
        Assert.Contains("EVERGREEN MODE", prompt);
        Assert.DoesNotContain("Use the web_search tool", prompt);
        Assert.Contains("must appear verbatim in the dossier", prompt);
    }

    [Fact]
    public void WriterUserPromptEmbedsDossier()
    {
        var settings = CreateSettings();
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2026, 8, 27), rng: new Random(1));

        var prompt = PromptBuilder.WriterUserPrompt(ctx, "## Research notes\n\nfindings here");

        Assert.Contains("findings here", prompt);
        Assert.Contains(settings.TopicHint, prompt);
    }

    [Fact]
    public void ShippedAppSettingsSatisfyValidation()
    {
        var settings = LoadShippedSettings();

        settings.Normalize();
        settings.Validate();

        Assert.NotEmpty(settings.VeniceBrainModel);
        Assert.True(settings.VeniceMaxTokens > 0);
    }

    private static GenerationSettings LoadShippedSettings()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "BlogGenerator", "appsettings.json")))
            dir = dir.Parent;

        Assert.NotNull(dir);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dir!.FullName, "BlogGenerator", "appsettings.json"))
            .Build();

        var settings = configuration.GetSection("Generation").Get<GenerationSettings>();
        Assert.NotNull(settings);
        return settings!;
    }
}
