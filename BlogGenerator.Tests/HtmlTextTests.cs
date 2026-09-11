using BlogGenerator.Core.Research;

namespace BlogGenerator.Tests;

public class HtmlTextTests
{
    [Fact]
    public void ToPlainTextStripsTagsAndDecodesEntities()
    {
        Assert.Equal(
            "Azure & .NET shipped \"it\"",
            HtmlText.ToPlainText("<p><strong>Azure</strong> &amp; .NET shipped &quot;it&quot;</p>"));
    }

    // A feed summary that carried its publisher's script tags would otherwise arrive as a wall of
    // JavaScript, which is both useless and expensive in context.
    [Fact]
    public void ToPlainTextDropsScriptAndStyleBodies()
    {
        var html = "<div>Real text<script>var x = 1; if (x < 2) {}</script><style>.a{color:red}</style></div>";

        var text = HtmlText.ToPlainText(html);

        Assert.Contains("Real text", text);
        Assert.DoesNotContain("var x", text);
        Assert.DoesNotContain("color:red", text);
    }

    [Fact]
    public void ToPlainTextKeepsBlockBoundariesAsLineBreaks()
    {
        Assert.Equal("Heading\n\nBody", HtmlText.ToPlainText("<h2>Heading</h2><p>Body</p>"));
    }

    [Fact]
    public void ToPlainTextCollapsesWhitespace()
    {
        Assert.Equal("one two three", HtmlText.ToPlainText("  one   two \n\t three  "));
    }

    // Publishers hard-wrap their markup constantly. Those newlines are insignificant whitespace in
    // HTML, so they must not survive as line breaks mid-sentence.
    [Fact]
    public void ToPlainTextTreatsSourceNewlinesAsInsignificantWhitespace()
    {
        Assert.Equal(
            "Azure AI Foundry shipped a new SDK today.",
            HtmlText.ToPlainText("<p>Azure AI Foundry shipped\n   a new SDK today.</p>"));
    }

    [Fact]
    public void ToPlainTextHandlesNullAndEmpty()
    {
        Assert.Equal("", HtmlText.ToPlainText(null));
        Assert.Equal("", HtmlText.ToPlainText("   "));
    }

    [Fact]
    public void TruncateLeavesShortTextAlone()
    {
        Assert.Equal("short", HtmlText.Truncate("short", 100));
    }

    // Truncation has to announce itself: a model handed a silently cut page will happily describe
    // the missing half as absent from the announcement.
    [Fact]
    public void TruncateSaysThatItTruncated()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 200));

        var truncated = HtmlText.Truncate(text, 100);

        Assert.Contains("[truncated after", truncated);
        Assert.True(truncated.Length < text.Length);
    }

    [Fact]
    public void TruncateCutsAtAWordBoundary()
    {
        var truncated = HtmlText.Truncate("alpha beta gamma delta epsilon", 14);

        Assert.StartsWith("alpha beta", truncated);
        Assert.DoesNotContain("gam\n", truncated);
    }
}
