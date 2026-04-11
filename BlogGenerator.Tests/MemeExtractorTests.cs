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
    public void ExtractsImgflipHintWithTwoTexts()
    {
        var md = "Some text.\n<!-- meme: template=Drake Hotline Bling, texts=\"Old SDK|New SDK\" -->\nMore text.";
        var hint = MemeExtractor.ExtractImgflipHint(md);
        Assert.NotNull(hint);
        Assert.Equal("Drake Hotline Bling", hint.TemplateName);
        Assert.Equal(2, hint.Texts.Count);
        Assert.Equal("Old SDK", hint.Texts[0]);
        Assert.Equal("New SDK", hint.Texts[1]);
    }

    [Fact]
    public void ExtractsImgflipHintWithFourTexts()
    {
        var md = "<!-- meme: template=Gru's Plan, texts=\"Step 1|Step 2|Step 3|Step 3 but wrong\" -->";
        var hint = MemeExtractor.ExtractImgflipHint(md);
        Assert.NotNull(hint);
        Assert.Equal("Gru's Plan", hint.TemplateName);
        Assert.Equal(4, hint.Texts.Count);
        Assert.Equal("Step 3 but wrong", hint.Texts[3]);
    }

    [Fact]
    public void ExtractImgflipHintIsCaseInsensitive()
    {
        var md = "<!-- MEME: template=This Is Fine, texts=\"prod is on fire|it is fine\" -->";
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
        var md = "Intro.\n<!-- meme: template=Drake Hotline Bling, texts=\"A|B\" -->\nOutro.";
        var cleaned = MemeExtractor.RemoveImgflipHint(md);
        Assert.DoesNotContain("<!-- meme:", cleaned);
        Assert.Contains("Intro.", cleaned);
        Assert.Contains("Outro.", cleaned);
    }

    [Fact]
    public void ReplacesImgflipHintInPlace()
    {
        var md = "Intro.\n<!-- meme: template=Drake Hotline Bling, texts=\"A|B\" -->\nOutro.";
        var result = MemeExtractor.ReplaceImgflipHint(md, "https://i.imgflip.com/abc.jpg", "My Post");
        Assert.DoesNotContain("<!-- meme:", result);
        Assert.Contains("![My Post meme](https://i.imgflip.com/abc.jpg)", result);
        // Position preserved — image sits between intro and outro
        var imageIndex = result.IndexOf("![");
        Assert.True(result.IndexOf("Intro.") < imageIndex);
        Assert.True(imageIndex < result.IndexOf("Outro."));
    }
}
