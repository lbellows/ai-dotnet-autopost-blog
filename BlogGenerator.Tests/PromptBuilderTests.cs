using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.PostGeneration;
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
        FoundryModels = ["gpt-5.4-mini", "gpt-5-mini"],
        FoundryDefaultModel = "gpt-5.4-mini",
        FoundryMaxTokens = 4096,
        CodeSamplesEnabled = true,
        CodeSampleMinLines = 15,
        CodeSampleMaxLines = 30,
    };

    [Fact]
    public void GuidanceCarriesTheConfiguredCodeSampleLength()
    {
        var settings = CreateSettings();
        settings.CodeSampleMinLines = 12;
        settings.CodeSampleMaxLines = 24;
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        Assert.Contains("12-24 lines", ctx.GuidanceBlock);
    }

    [Fact]
    public void GuidancePrefersOmittingCodeOverInventingIt()
    {
        var ctx = PromptBuilder.Build(CreateSettings(), today: new DateOnly(2025, 6, 2));
        Assert.Contains("OMIT the code block entirely", ctx.GuidanceBlock);
        Assert.Contains("Never label a sample as pseudocode", ctx.GuidanceBlock);
    }

    [Fact]
    public void DisablingCodeSamplesSwapsTheGuidance()
    {
        var settings = CreateSettings();
        settings.CodeSamplesEnabled = false;
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        Assert.Contains("Do not include code blocks", ctx.GuidanceBlock);
        Assert.DoesNotContain("centerpiece", ctx.GuidanceBlock);
    }

    // The Venice writer stage composes its own system prompt; it must inherit the same rules.
    [Fact]
    public void WriterSystemPromptInheritsCodeGuidance()
    {
        var settings = CreateSettings();
        var ctx = PromptBuilder.Build(settings, today: new DateOnly(2025, 6, 2));
        var writerPrompt = PromptBuilder.WriterSystemPrompt(ctx, settings);
        Assert.Contains("AT MOST ONE code block", writerPrompt);
    }

    [Fact]
    public void BuildProducesSundaySynopsisMode()
    {
        var settings = CreateSettings();
        var sunday = new DateOnly(2025, 6, 1); // Sunday
        var ctx = PromptBuilder.Build(settings, today: sunday);
        Assert.Contains("synopsis day", ctx.UserPrompt);
    }

    [Fact]
    public void BuildProducesWeekdayDeepDiveMode()
    {
        var settings = CreateSettings();
        var monday = new DateOnly(2025, 6, 2); // Monday
        var ctx = PromptBuilder.Build(settings, today: monday);
        Assert.Contains("laser-focused", ctx.UserPrompt);
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
        Assert.Contains("Primary requested link: https://example.com/article", ctx.UserPrompt);
        Assert.Contains("5)", ctx.UserPrompt); // extra instruction added
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
    public void ImgflipGuidanceListsEveryTemplateRegardlessOfShuffle()
    {
        var guidance = PromptBuilder.ImgflipGuidance(new Random(12345));
        foreach (var template in PromptBuilder.ImgflipTemplateCatalog)
        {
            // Each catalog entry is "Name(boxes...)"; assert the human-readable name survives.
            var name = template[..template.IndexOf('(')];
            Assert.Contains(name, guidance);
        }
    }

    [Fact]
    public void ImgflipGuidanceShuffleVariesTemplateOrderAcrossSeeds()
    {
        // Different RNG seeds should present the templates in a different order, which is
        // what breaks the model's bias toward whatever sits first in a fixed list.
        var a = PromptBuilder.ImgflipGuidance(new Random(1));
        var b = PromptBuilder.ImgflipGuidance(new Random(2));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ImgflipGuidanceShuffleIsDeterministicForAGivenSeed()
    {
        Assert.Equal(
            PromptBuilder.ImgflipGuidance(new Random(42)),
            PromptBuilder.ImgflipGuidance(new Random(42)));
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

    private static readonly IReadOnlyList<PublishedPost> RecentPosts =
    [
        new(new DateOnly(2026, 9, 15), "PTU or Pay-As-You-Go? Picking the Right Deployment Model"),
        new(new DateOnly(2026, 9, 13), "Your Agent's Token Bill Is a Context Problem"),
    ];

    // The repetition this exists to stop: without the archive in the prompt, three runs in five days
    // wrote the same Azure OpenAI pricing post from the same four sources.
    [Fact]
    public void SystemPromptListsTheAlreadyPublishedPosts()
    {
        var ctx = PromptBuilder.Build(
            CreateSettings(), today: new DateOnly(2026, 9, 18), recentPosts: RecentPosts);

        Assert.Contains("Already published on this blog", ctx.SystemPrompt);
        Assert.Contains("2026-09-15 — PTU or Pay-As-You-Go?", ctx.SystemPrompt);
        Assert.Contains("do not reuse their title structures", ctx.SystemPrompt);
    }

    // The writer stage never sees the single-call system prompt, and it is the stage that picks
    // the topic on the Venice and local paths.
    [Fact]
    public void WriterSystemPromptListsTheAlreadyPublishedPosts()
    {
        var settings = CreateSettings();
        var ctx = PromptBuilder.Build(
            settings, today: new DateOnly(2026, 9, 18), recentPosts: RecentPosts);

        var prompt = PromptBuilder.WriterSystemPrompt(ctx, settings);

        Assert.Contains("Already published on this blog", prompt);
        Assert.Contains("Your Agent's Token Bill Is a Context Problem", prompt);
    }

    // Telling only the writer to avoid a topic while the research stage keeps bringing back that
    // topic produces a worse post, not a different one.
    [Fact]
    public void ResearchPromptsAskForGroundTheArchiveDoesNotCover()
    {
        var settings = CreateSettings();
        var today = new DateOnly(2026, 9, 18);
        var start = today.AddDays(-settings.RecentWindowDays);

        var research = PromptBuilder.ResearchSystemPrompt(settings, today, start, RecentPosts);
        var feedResearch = PromptBuilder.FeedResearchSystemPrompt(settings, today, start, RecentPosts);

        Assert.Contains("open ground those have not covered", research);
        Assert.Contains("open ground those have not covered", feedResearch);
        Assert.Contains("2026-09-13 — Your Agent's Token Bill", feedResearch);
    }

    [Fact]
    public void PromptsOmitTheCoverageSectionWhenThereIsNoArchive()
    {
        var settings = CreateSettings();
        var today = new DateOnly(2026, 9, 18);
        var ctx = PromptBuilder.Build(settings, today: today);

        Assert.DoesNotContain("Already published on this blog", ctx.SystemPrompt);
        Assert.DoesNotContain("Already published on this blog", PromptBuilder.WriterSystemPrompt(ctx, settings));
        Assert.DoesNotContain(
            "Already published on this blog",
            PromptBuilder.ResearchSystemPrompt(settings, today, today.AddDays(-2)));
        // And leave no gap where the section would have been.
        Assert.DoesNotContain("\n\n\n", ctx.SystemPrompt);
    }

}
