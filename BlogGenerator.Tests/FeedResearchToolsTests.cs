using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Research;

namespace BlogGenerator.Tests;

public class FeedResearchToolsTests
{
    private static readonly DateOnly Today = new(2026, 9, 11);
    private static readonly DateOnly WindowStart = new(2026, 9, 8);

    private static readonly FeedItem InWindow = new(
        "dotnet", ".NET 10 is generally available", "https://devblogs.microsoft.com/dotnet/ga/",
        new DateTimeOffset(2026, 9, 10, 17, 0, 0, TimeSpan.Zero), "It shipped.");

    private static readonly FeedItem Older = new(
        "infoq", "A retrospective on ANN indexes", "https://www.infoq.com/articles/ann/",
        new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero), "Background reading.");

    private static readonly FeedItem Undated = new(
        "github", "Something undated", "https://github.blog/undated/", null, "No date given.");

    [Fact]
    public void FormatSplitsTheWindowFromTheBackground()
    {
        var text = FeedResearchTools.Format([InWindow, Older], Today, WindowStart, includeOlder: true);

        Assert.Contains("IN WINDOW (1)", text);
        Assert.Contains("2026-09-10 | dotnet | .NET 10 is generally available", text);
        Assert.Contains("OLDER (1)", text);
        Assert.Contains("2026-07-02 | infoq |", text);
    }

    // Deciding the window here rather than in the prompt is the point of the tool: local models are
    // unreliable at date arithmetic, and this stage exists to answer a date question.
    [Fact]
    public void FormatStatesTheWindowItApplied()
    {
        var text = FeedResearchTools.Format([InWindow], Today, WindowStart, includeOlder: false);

        Assert.Contains("Today is 2026-09-11", text);
        Assert.Contains("freshness window is 2026-09-08 to 2026-09-11", text);
    }

    [Fact]
    public void FormatHidesOlderItemsByDefaultButSaysHowManyThereAre()
    {
        var text = FeedResearchTools.Format([InWindow, Older], Today, WindowStart, includeOlder: false);

        Assert.DoesNotContain("infoq", text);
        Assert.Contains("1 older articles not shown", text);
    }

    [Fact]
    public void FormatSaysPlainlyWhenNothingLandedInTheWindow()
    {
        var text = FeedResearchTools.Format([Older], Today, WindowStart, includeOlder: false);

        Assert.Contains("IN WINDOW (0)", text);
        Assert.Contains("nothing published in the window matched", text);
    }

    // An undated item cannot be shown to be inside the window, and "probably recent" is exactly the
    // guess that produces a post claiming something shipped this week when it did not.
    [Fact]
    public void FormatTreatsUndatedItemsAsOutsideTheWindow()
    {
        var text = FeedResearchTools.Format([Undated], Today, WindowStart, includeOlder: true);

        Assert.Contains("IN WINDOW (0)", text);
        Assert.Contains("undated | github | Something undated", text);
    }

    [Fact]
    public void FormatOrdersNewestFirst()
    {
        var newer = InWindow with { Title = "Newer", Published = new DateTimeOffset(2026, 9, 11, 8, 0, 0, TimeSpan.Zero) };

        var text = FeedResearchTools.Format([InWindow, newer], Today, WindowStart, includeOlder: false);

        Assert.True(text.IndexOf("Newer", StringComparison.Ordinal) <
                    text.IndexOf(".NET 10 is generally available", StringComparison.Ordinal));
    }

    [Fact]
    public void FilterMatchesTitleOrSummaryOnAnyKeyword()
    {
        Assert.Single(FeedResearchTools.Filter([InWindow, Older], "ANN"));
        Assert.Single(FeedResearchTools.Filter([InWindow, Older], "shipped"));
        Assert.Equal(2, FeedResearchTools.Filter([InWindow, Older], "ann dotnet .NET").Count);
    }

    [Fact]
    public void FilterKeepsEverythingWhenThereIsNoQuery()
    {
        Assert.Equal(2, FeedResearchTools.Filter([InWindow, Older], null).Count);
        Assert.Equal(2, FeedResearchTools.Filter([InWindow, Older], "   ").Count);
    }

    [Theory]
    [InlineData("https://devblogs.microsoft.com/dotnet/ga/", true)]
    [InlineData("https://github.com/dotnet/core/releases/tag/v10", true)]
    [InlineData("https://learn.microsoft.com/dotnet/whats-new", true)]
    [InlineData("https://random-blog.example.com/post", false)]
    [InlineData("https://notinfoq.com/post", false)]
    [InlineData("https://evil.example/devblogs.microsoft.com", false)]
    public void IsAllowedAcceptsFeedHostsAndAllowedDomainsOnly(string url, bool expected)
    {
        Assert.Equal(expected, FeedResearchTools.IsAllowed(new Uri(url), Settings(), NothingSeen));
    }

    // A publisher's feed and its articles often live on different hosts — feed.infoq.com serves
    // links to www.infoq.com — so what the feed actually handed back is allowed on its own terms,
    // without guessing which parent domain a host belongs to.
    [Fact]
    public void IsAllowedAcceptsAUrlAFeedActuallyListed()
    {
        var article = new Uri("https://www.infoq.com/articles/ann/");

        Assert.False(FeedResearchTools.IsAllowed(article, Settings(), NothingSeen));
        Assert.True(FeedResearchTools.IsAllowed(article, Settings(), Seen(article.ToString())));
    }

    [Fact]
    public void IsAllowedHonoursTheBlockedDomainListEvenForSomethingAFeedListed()
    {
        var settings = Settings();
        settings.BlockedDomains = ["infoq.com"];
        var article = "https://www.infoq.com/articles/ann/";

        Assert.False(FeedResearchTools.IsAllowed(new Uri(article), settings, Seen(article)));
    }

    private static readonly IReadOnlySet<string> NothingSeen = new HashSet<string>();

    private static IReadOnlySet<string> Seen(params string[] urls) =>
        new HashSet<string>(urls, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task InvokeReportsAnUnknownToolRatherThanThrowing()
    {
        var answer = await Tools().InvokeAsync("search_the_web", "{}");

        Assert.Contains("no tool named 'search_the_web'", answer);
        Assert.Contains("list_recent_posts", answer);
    }

    [Fact]
    public async Task InvokeListsTheRealSourceNamesWhenAskedForOneThatDoesNotExist()
    {
        var answer = await Tools().InvokeAsync("list_recent_posts", """{"source":"hacker-news"}""");

        Assert.Contains("no source named 'hacker-news'", answer);
        Assert.Contains("dotnet", answer);
    }

    // A model that invents a URL must be told no here, not discover it in the published post.
    [Fact]
    public async Task FetchPageRefusesAHostTheBlogDoesNotDrawFrom()
    {
        var answer = await Tools().InvokeAsync("fetch_page", """{"url":"https://random-blog.example.com/post"}""");

        Assert.Contains("not a source this blog draws from", answer);
    }

    [Fact]
    public async Task FetchPageRejectsSomethingThatIsNotAUrl()
    {
        Assert.Contains(
            "not an absolute http(s) URL",
            await Tools().InvokeAsync("fetch_page", """{"url":"the dotnet blog"}"""));
    }

    [Fact]
    public async Task FetchPageSaysWhatItNeedsWhenTheArgumentIsMissing()
    {
        Assert.Contains("needs a url argument", await Tools().InvokeAsync("fetch_page", "{}"));
    }

    // Small models emit malformed argument JSON often enough that it has to be an ordinary event.
    [Fact]
    public async Task InvokeToleratesArgumentsThatAreNotValidJson()
    {
        Assert.Contains("needs a url argument", await Tools().InvokeAsync("fetch_page", "{url: broken"));
    }

    [Fact]
    public void SchemasDescribeBothToolsAndNameTheAvailableSources()
    {
        var schemas = Tools().Schemas;

        Assert.Equal(2, schemas.Count);
        var names = schemas
            .Select(schema => (Dictionary<string, object>)((Dictionary<string, object>)schema)["function"])
            .Select(function => (string)function["name"])
            .ToList();

        Assert.Equal(["list_recent_posts", "fetch_page"], names);

        var listDescription = (string)((Dictionary<string, object>)
            ((Dictionary<string, object>)schemas[0])["function"])["description"];
        Assert.Contains("dotnet, infoq", listDescription);
    }

    private static FeedResearchTools Tools() => new(new HttpClient(), Settings());

    private static GenerationSettings Settings()
    {
        var settings = new GenerationSettings
        {
            AllowedDomains = ["learn.microsoft.com"],
            ResearchFeedItemsPerSource = 15,
            ResearchPageMaxChars = 12000,
            ResearchFeeds =
            [
                new FeedSource { Name = "dotnet", Url = "https://devblogs.microsoft.com/dotnet/feed/" },
                new FeedSource { Name = "infoq", Url = "https://feed.infoq.com/" },
                new FeedSource { Name = "dotnet-releases", Url = "https://github.com/dotnet/core/releases.atom" },
            ],
        };

        return settings;
    }
}
