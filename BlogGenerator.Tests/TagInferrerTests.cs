using BlogGenerator.Core.PostGeneration;

namespace BlogGenerator.Tests;

public class TagInferrerTests
{
    [Fact]
    public void InfersTagsFromHeadings()
    {
        var md = "# Azure OpenAI Gets Faster\n## Performance Benchmarks\n**TL;DR** Azure is faster now.";
        var tags = TagInferrer.Infer(md, "claude-sonnet-4-6");
        Assert.NotEmpty(tags);
        Assert.Contains("azure", tags);
    }

    [Fact]
    public void AddsAiTagIfPresent()
    {
        var md = "# Some AI News\nAI is everywhere.";
        var tags = TagInferrer.Infer(md, "gpt-4");
        Assert.Contains("ai", tags);
    }

    [Fact]
    public void AddsModelTag()
    {
        var md = "# Some News\nContent here.";
        var tags = TagInferrer.Infer(md, "claude-sonnet-4-6");
        Assert.Contains("claude-sonnet-4-6", tags);
    }

    [Fact]
    public void CapsAtSixTags()
    {
        var md = "# Alpha Beta Gamma Delta Epsilon Zeta Eta Theta\n" +
                 "## Iota Kappa Lambda Mu\nSome AI content.";
        var tags = TagInferrer.Infer(md, "mymodel");
        Assert.True(tags.Count <= 6);
    }

    [Fact]
    public void NormalizeTagFiltersStopwords()
    {
        Assert.Equal("", TagInferrer.NormalizeTag("the"));
        Assert.Equal("", TagInferrer.NormalizeTag("and"));
        Assert.Equal("", TagInferrer.NormalizeTag("developers"));
    }

    [Theory]
    [InlineData("between")]
    [InlineData("bring")]
    [InlineData("can")]
    [InlineData("should")]
    [InlineData("more")]
    public void NormalizeTagFiltersGenericEnglish(string token)
    {
        Assert.Equal("", TagInferrer.NormalizeTag(token));
    }

    [Theory]
    [InlineData("azure")]
    [InlineData("copilot")]
    [InlineData("byok")]
    [InlineData("agent")]
    [InlineData("foundry")]
    [InlineData("mcp")]
    public void NormalizeTagKeepsTechnicalTerms(string token)
    {
        Assert.Equal(token, TagInferrer.NormalizeTag(token));
    }

    [Fact]
    public void InferDropsFillerWordsFromHeadings()
    {
        var md = "# Choosing Between Copilot and Foundry\n" +
                 "## What You Can Bring to Azure\n" +
                 "Some AI content about Copilot.";

        var tags = TagInferrer.Infer(md, "claude-sonnet-5");

        Assert.DoesNotContain("between", tags);
        Assert.DoesNotContain("bring", tags);
        Assert.DoesNotContain("can", tags);
        Assert.Contains("copilot", tags);
    }

    [Fact]
    public void NormalizeTagFiltersShortTokens()
    {
        Assert.Equal("", TagInferrer.NormalizeTag("ab"));
    }

    [Theory]
    [InlineData("github.blog")]
    [InlineData("learn.microsoft.com")]
    [InlineData("openai.com")]
    [InlineData("azure.microsoft.com")]
    public void LooksLikeDomainDetectsSourceHosts(string token)
    {
        Assert.True(TagInferrer.LooksLikeDomain(token));
    }

    [Theory]
    [InlineData(".net")]
    [InlineData("asp.net")]
    [InlineData("gpt-5.6")]
    [InlineData("copilot")]
    public void LooksLikeDomainSparesPlatformNames(string token)
    {
        Assert.False(TagInferrer.LooksLikeDomain(token));
    }

    [Fact]
    public void NormalizeTagRejectsDomainsButKeepsDotNet()
    {
        Assert.Equal("", TagInferrer.NormalizeTag("github.blog"));
        Assert.Equal("", TagInferrer.NormalizeTag("learn.microsoft.com"));
        Assert.Equal(".net", TagInferrer.NormalizeTag(".NET"));
        Assert.Equal("asp.net", TagInferrer.NormalizeTag("ASP.NET"));
    }

    [Fact]
    public void SalientTokensTakeMidSentenceCapitalsNotSentenceStarts()
    {
        var salient = TagInferrer.CollectSalientTokens(
            "Teams shipping on Azure use Copilot daily.\nInstead, they wait.");

        Assert.Contains("azure", salient);
        Assert.Contains("copilot", salient);
        Assert.DoesNotContain("teams", salient);   // opens a line
        Assert.DoesNotContain("instead", salient); // opens a line
    }

    [Fact]
    public void SalientTokensIgnoreHeadingsAndUrls()
    {
        var salient = TagInferrer.CollectSalientTokens(
            "# Everything Here Is Title Case\nSee https://github.blog/some-post-slug for detail.");

        Assert.DoesNotContain("everything", salient);
        Assert.DoesNotContain("title", salient);
        Assert.DoesNotContain("some-post-slug", salient);
    }

    [Fact]
    public void InferPrefersProductNamesOverFillerInConversationalHeadings()
    {
        var md = "# Do You Actually Need an AI Gateway?\n" +
                 "## What It Actually Does\n" +
                 "An API gateway already sits in front of Azure OpenAI for most teams. " +
                 "Azure meters it, and Azure bills it. The OpenAI SDK is unaffected.\n\n" +
                 "## Further reading\n" +
                 "https://learn.microsoft.com/en-us/azure/api-management/ai-gateway-overview";

        var tags = TagInferrer.Infer(md, "claude-sonnet-5");

        Assert.Contains("azure", tags);
        Assert.DoesNotContain("actually", tags);
        Assert.DoesNotContain("already", tags);
        Assert.DoesNotContain("learn.microsoft.com", tags);
        Assert.DoesNotContain("en-us", tags);
    }

    [Fact]
    public void NormalizeTagFormatsCorrectly()
    {
        Assert.Equal("azure", TagInferrer.NormalizeTag("Azure"));
        Assert.Equal(".net", TagInferrer.NormalizeTag(".NET"));
    }
}
