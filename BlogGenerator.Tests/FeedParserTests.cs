using BlogGenerator.Core.Research;

namespace BlogGenerator.Tests;

public class FeedParserTests
{
    private const string Rss = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/"
             xmlns:content="http://purl.org/rss/1.0/modules/content/">
          <channel>
            <title>The .NET Blog</title>
            <item>
              <title>.NET 10 is generally available</title>
              <link>https://devblogs.microsoft.com/dotnet/dotnet-10-ga/</link>
              <pubDate>Thu, 10 Sep 2026 17:00:00 +0000</pubDate>
              <description>&lt;p&gt;It shipped &amp;amp; here is what changed.&lt;/p&gt;</description>
            </item>
            <item>
              <title>An older post</title>
              <link>https://devblogs.microsoft.com/dotnet/older/</link>
              <dc:date>2026-08-01T09:30:00Z</dc:date>
              <content:encoded>Long body text.</content:encoded>
            </item>
          </channel>
        </rss>
        """;

    private const string Atom = """
        <?xml version="1.0" encoding="UTF-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
          <title>Releases</title>
          <entry>
            <title>v10.0.0</title>
            <link rel="alternate" type="text/html" href="https://github.com/dotnet/core/releases/tag/v10.0.0"/>
            <published>2026-09-08T20:34:32Z</published>
            <updated>2026-09-09T11:00:00Z</updated>
            <summary>Release notes.</summary>
          </entry>
        </feed>
        """;

    [Fact]
    public void ParsesRssItems()
    {
        var items = FeedParser.Parse("dotnet", Rss);

        Assert.Equal(2, items.Count);
        var first = items[0];
        Assert.Equal("dotnet", first.Source);
        Assert.Equal(".NET 10 is generally available", first.Title);
        Assert.Equal("https://devblogs.microsoft.com/dotnet/dotnet-10-ga/", first.Url);
        Assert.Equal(new DateTimeOffset(2026, 9, 10, 17, 0, 0, TimeSpan.Zero), first.Published);
        Assert.Equal("It shipped & here is what changed.", first.Summary);
    }

    // Publishers mix pubDate, dc:date and the Atom elements freely, sometimes across feeds from the
    // same company, so the parser has to read whichever one is present.
    [Fact]
    public void ReadsDublinCoreDatesWhenThereIsNoPubDate()
    {
        var items = FeedParser.Parse("dotnet", Rss);

        Assert.Equal(new DateTimeOffset(2026, 8, 1, 9, 30, 0, TimeSpan.Zero), items[1].Published);
    }

    [Fact]
    public void ParsesAtomEntriesIncludingTheLinkAttribute()
    {
        var items = FeedParser.Parse("dotnet-releases", Atom);

        var entry = Assert.Single(items);
        Assert.Equal("v10.0.0", entry.Title);
        Assert.Equal("https://github.com/dotnet/core/releases/tag/v10.0.0", entry.Url);
        Assert.Equal("Release notes.", entry.Summary);
    }

    // An entry edited after the fact still announced on its original date, and the freshness window
    // is about the announcement.
    [Fact]
    public void PrefersPublishedOverUpdated()
    {
        var entry = Assert.Single(FeedParser.Parse("dotnet-releases", Atom));

        Assert.Equal(new DateTimeOffset(2026, 9, 8, 20, 34, 32, TimeSpan.Zero), entry.Published);
    }

    [Fact]
    public void SkipsEntriesWithNoUsableLink()
    {
        var xml = """
            <rss version="2.0"><channel>
              <item><title>No link at all</title><pubDate>Thu, 10 Sep 2026 17:00:00 GMT</pubDate></item>
              <item><title>Has one</title><link>https://example.com/a</link></item>
            </channel></rss>
            """;

        var items = FeedParser.Parse("x", xml);

        Assert.Equal("Has one", Assert.Single(items).Title);
    }

    [Fact]
    public void FallsBackToAPermalinkGuid()
    {
        var xml = """
            <rss version="2.0"><channel>
              <item><title>Guid only</title><guid isPermaLink="true">https://example.com/g</guid></item>
            </channel></rss>
            """;

        Assert.Equal("https://example.com/g", Assert.Single(FeedParser.Parse("x", xml)).Url);
    }

    [Fact]
    public void LeavesPublishedNullWhenTheFeedGivesNoDate()
    {
        var xml = """
            <rss version="2.0"><channel>
              <item><title>Undated</title><link>https://example.com/a</link></item>
            </channel></rss>
            """;

        Assert.Null(Assert.Single(FeedParser.Parse("x", xml)).Published);
    }

    // A publisher serving an error page instead of XML must fail with something that names the
    // feed, not a bare XmlException from deep inside a research run.
    [Fact]
    public void RejectsContentThatIsNotXmlWithAMessageNamingTheFeed()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => FeedParser.Parse("techcrunch", "<!DOCTYPE html><html><body>429</body>"));

        Assert.Contains("techcrunch", ex.Message);
    }
}
