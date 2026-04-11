using BlogGenerator.Core.Memes;

namespace BlogGenerator.Tests;

public class MemeExtractorTests
{
    [Fact]
    public void ExtractsTldrFromSameLine()
    {
        var md = "# Title\n**TL;DR** Azure is great.\nMore content.";
        Assert.Equal("Azure is great.", MemeExtractor.ExtractTldrLine(md));
    }

    [Fact]
    public void ExtractsTldrFromNextLine()
    {
        var md = "# Title\n**TL;DR**\nAzure is great.\nMore content.";
        Assert.Equal("Azure is great.", MemeExtractor.ExtractTldrLine(md));
    }

    [Fact]
    public void ReturnsNullWhenNoTldr()
    {
        var md = "# Title\nNo summary here.";
        Assert.Null(MemeExtractor.ExtractTldrLine(md));
    }

    // ── ImgflipHint extraction ───────────────────────────────────────────────

    [Fact]
    public void ExtractsImgflipHintWithAllFields()
    {
        var md = "Some text.\n<!-- meme: template=Drake Hotline Bling, top=\"Old SDK\", bottom=\"New SDK\" -->\nMore text.";
        var hint = MemeExtractor.ExtractImgflipHint(md);
        Assert.NotNull(hint);
        Assert.Equal("Drake Hotline Bling", hint.TemplateName);
        Assert.Equal("Old SDK", hint.TopText);
        Assert.Equal("New SDK", hint.BottomText);
    }

    [Fact]
    public void ExtractImgflipHintIsCaseInsensitive()
    {
        var md = "<!-- MEME: template=This Is Fine, top=\"prod is on fire\", bottom=\"it is fine\" -->";
        var hint = MemeExtractor.ExtractImgflipHint(md);
        Assert.NotNull(hint);
        Assert.Equal("This Is Fine", hint.TemplateName);
    }

    [Fact]
    public void ReturnsNullWhenNoImgflipHint()
    {
        var md = "# Title\nNo hint here.";
        Assert.Null(MemeExtractor.ExtractImgflipHint(md));
    }

    [Fact]
    public void RemovesImgflipHintFromMarkdown()
    {
        var md = "Intro.\n<!-- meme: template=Drake Hotline Bling, top=\"A\", bottom=\"B\" -->\nOutro.";
        var cleaned = MemeExtractor.RemoveImgflipHint(md);
        Assert.DoesNotContain("<!-- meme:", cleaned);
        Assert.Contains("Intro.", cleaned);
        Assert.Contains("Outro.", cleaned);
    }
}
