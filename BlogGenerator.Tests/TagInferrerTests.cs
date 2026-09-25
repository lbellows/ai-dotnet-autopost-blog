using BlogGenerator.Core.PostGeneration;

namespace BlogGenerator.Tests;

public class TagInferrerTests
{
    [Fact]
    public void InfersTagsFromHeadings()
    {
        var md = "# Azure OpenAI Gets Faster\n## Performance Benchmarks\n**TL;DR** Azure is faster now.";
        var tags = TagInferrer.Infer(md, ["claude-sonnet-4-6"]);
        Assert.NotEmpty(tags);
        Assert.Contains("azure", tags);
    }

    [Fact]
    public void IgnoresIdentifiersInsideCodeBlocks()
    {
        var md = "# Retry Policies in Azure API Management\nAzure APIM retries on throttling.\n\n```xml\n<retry condition=\"@(context.Response.StatusCode == 429)\" />\n```\n";

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5"]);

        Assert.DoesNotContain(tags, t => t.Contains("statuscode"));
        Assert.Contains("azure", tags);
    }

    [Fact]
    public void CommentLinesInCodeBlocksAreNotHeadings()
    {
        var md = "# Azure Functions Cold Starts\n\n```bash\n# Kubernetes Kubernetes Kubernetes\naz functionapp create\n```\n";

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5"]);

        Assert.DoesNotContain("kubernetes", tags);
    }

    [Fact]
    public void AddsAiTagIfPresent()
    {
        var md = "# Some AI News\nAI is everywhere.";
        var tags = TagInferrer.Infer(md, ["gpt-4"]);
        Assert.Contains("ai", tags);
    }

    [Fact]
    public void AddsModelTag()
    {
        var md = "# Some News\nContent here.";
        var tags = TagInferrer.Infer(md, ["claude-sonnet-4-6"]);
        Assert.Contains("claude-sonnet-4-6", tags);
    }

    [Fact]
    public void TagsEveryModelInABrainWriterRun()
    {
        var md = "# Some News\nContent here.";

        var tags = TagInferrer.Infer(md, ["grok-4-6", "claude-sonnet-5"]);

        Assert.Contains("grok-4-6", tags);
        Assert.Contains("claude-sonnet-5", tags);
    }

    [Fact]
    public void ModelTagsSurviveTheCapAtTopicTagsExpense()
    {
        var md = "# Alpha Beta Gamma Delta Epsilon Zeta Eta Theta\n" +
                 "## Iota Kappa Lambda Mu\nSome AI content.";

        var tags = TagInferrer.Infer(md, ["grok-4-6", "claude-sonnet-5"]);

        Assert.True(tags.Count <= 6, $"expected at most 6 tags, got [{string.Join(", ", tags)}]");
        Assert.Contains("grok-4-6", tags);
        Assert.Contains("claude-sonnet-5", tags);
    }

    [Fact]
    public void OneModelStillLeavesRoomForFiveTopicTags()
    {
        var md = "# Alpha Beta Gamma Delta Epsilon Zeta Eta Theta\n" +
                 "## Iota Kappa Lambda Mu\nSome AI content.";

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5"]);

        Assert.Equal(6, tags.Count);
        Assert.Equal("claude-sonnet-5", tags[^1]);
    }

    [Fact]
    public void ProvenanceTagsAreKeptAndSitInFrontOfTheModelTag()
    {
        var md = "# Alpha Beta Gamma Delta Epsilon Zeta Eta Theta\n" +
                 "## Iota Kappa Lambda Mu\nSome AI content.";

        var tags = TagInferrer.Infer(md, ["gemma-26b"], ["local"]);

        Assert.True(tags.Count <= 6, $"expected at most 6 tags, got [{string.Join(", ", tags)}]");
        Assert.Equal(["local", "gemma-26b"], tags[^2..]);
    }

    [Fact]
    public void ProvenanceTagCostsATopicSlotRatherThanDroppingOffTheEnd()
    {
        var md = "# Alpha Beta Gamma Delta Epsilon Zeta Eta Theta\n" +
                 "## Iota Kappa Lambda Mu\nSome AI content.";

        var withoutProvenance = TagInferrer.Infer(md, ["gemma-26b"]);
        var withProvenance = TagInferrer.Infer(md, ["gemma-26b"], ["local"]);

        // Same total; the provenance tag is paid for out of the topics, not appended past the cap.
        Assert.Equal(6, withoutProvenance.Count);
        Assert.Equal(6, withProvenance.Count);
        Assert.Equal(5, withoutProvenance.Count(t => t != "gemma-26b"));
        Assert.Equal(4, withProvenance.Count(t => t is not ("gemma-26b" or "local")));
        Assert.DoesNotContain("local", withoutProvenance);
    }

    [Fact]
    public void ProvenanceTagIsNotDuplicatedWhenItAlsoNamesAModel()
    {
        var md = "# Some News\nContent here.";

        var tags = TagInferrer.Infer(md, ["local"], ["local", "Local"]);

        Assert.Single(tags, t => t == "local");
    }

    [Fact]
    public void NoProvenanceTagsLeavesTheTagListUnchanged()
    {
        var md = "# Some News\nContent here.";

        Assert.Equal(TagInferrer.Infer(md, ["claude-sonnet-5"]), TagInferrer.Infer(md, ["claude-sonnet-5"], []));
        Assert.Equal(TagInferrer.Infer(md, ["claude-sonnet-5"]), TagInferrer.Infer(md, ["claude-sonnet-5"], null));
    }

    // Front matter writes tags as a YAML flow sequence, so a colon in a local model id
    // ("qwen3:32b") would silently turn the sequence into a mapping.
    [Theory]
    [InlineData("qwen3:32b", "qwen3-32b")]
    [InlineData("hf.co/unsloth/gpt-oss-20b-GGUF", "hf.co-unsloth-gpt-oss-20b-gguf")]
    [InlineData("claude-sonnet-5", "claude-sonnet-5")]
    [InlineData("gpt-oss-20b", "gpt-oss-20b")]
    public void ModelTagsAreSafeForYamlFlowSequences(string model, string expected)
    {
        Assert.Equal([expected], TagInferrer.NormalizeModelTags([model]));
    }

    [Fact]
    public void RepeatedModelNameIsTaggedOnce()
    {
        var md = "# Some News\nContent here.";

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5", "Claude-Sonnet-5"]);

        Assert.Single(tags, t => t == "claude-sonnet-5");
    }

    [Fact]
    public void FallsBackToTheDefaultModelTagWhenNoneReported()
    {
        var md = "# Some News\nContent here.";

        Assert.Contains("claude", TagInferrer.Infer(md, []));
        Assert.Contains("claude", TagInferrer.Infer(md, null));
    }

    [Fact]
    public void CapsAtSixTags()
    {
        var md = "# Alpha Beta Gamma Delta Epsilon Zeta Eta Theta\n" +
                 "## Iota Kappa Lambda Mu\nSome AI content.";
        var tags = TagInferrer.Infer(md, ["mymodel"]);
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

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5"]);

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

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5"]);

        Assert.Contains("azure", tags);
        Assert.DoesNotContain("actually", tags);
        Assert.DoesNotContain("already", tags);
        Assert.DoesNotContain("learn.microsoft.com", tags);
        Assert.DoesNotContain("en-us", tags);
    }

    [Theory]
    [InlineData("everywhere.", "everywhere")]
    [InlineData("request.", "request")]
    [InlineData("gateway-", "gateway")]
    public void NormalizeTagDropsSentencePunctuation(string token, string expected)
    {
        Assert.Equal(expected, TagInferrer.NormalizeTag(token));
    }

    [Theory]
    [InlineData("vs.")]   // two letters once the dot goes; too short to tag
    [InlineData("a.")]
    [InlineData("not.")]  // a stopword the trailing dot used to hide
    public void NormalizeTagRejectsPunctuatedFillerOnceTrimmed(string token)
    {
        Assert.Equal("", TagInferrer.NormalizeTag(token));
    }

    [Theory]
    [InlineData(".net", ".net")]
    [InlineData("asp.net", "asp.net")]
    [InlineData("gpt-5.4", "gpt-5.4")]
    [InlineData("c++", "c++")]
    public void NormalizeTagKeepsPunctuationThatBelongsToTheName(string token, string expected)
    {
        Assert.Equal(expected, TagInferrer.NormalizeTag(token));
    }

    [Fact]
    public void SalientTokensIgnoreWordsMadeIdentifierLikeByASentenceDot()
    {
        var salient = TagInferrer.CollectSalientTokens("It runs on Azure everywhere.");

        Assert.Contains("azure", salient);
        Assert.DoesNotContain("everywhere", salient);
        Assert.DoesNotContain("everywhere.", salient);
    }

    [Fact]
    public void InferDoesNotEmitPunctuatedTags()
    {
        var md = "# Azure Ships An Agent Gateway\n\n" +
                 "Azure shipped it, and Copilot picked it up. AI everywhere.";

        var tags = TagInferrer.Infer(md, ["claude-sonnet-5"]);

        Assert.DoesNotContain(tags, t => t.EndsWith('.'));
        Assert.Contains("azure", tags);
        Assert.Contains("copilot", tags);
    }

    // Regression: "A 4,000-character chunk" shipped "000-character" as a tag on a published post.
    // The token pattern has no comma, so the number splits and the fragment looks like a version.
    [Theory]
    [InlineData("000-character")]
    [InlineData("500-ms")]
    [InlineData("64-bit")]
    [InlineData("30-80")]
    [InlineData("1.5-x")]
    public void NormalizeTagRejectsMeasurements(string token)
    {
        Assert.Equal("", TagInferrer.NormalizeTag(token));
    }

    // Only a leading digit run marks a measurement; a name that merely contains one is a name.
    [Theory]
    [InlineData("gpt-5")]
    [InlineData("gpt-5.4")]
    [InlineData(".net")]
    [InlineData("asp.net")]
    [InlineData("claude-sonnet-5")]
    [InlineData("qwen38-27b")]
    [InlineData("gemma-26b")]
    public void NormalizeTagKeepsNamesThatCarryDigits(string token)
    {
        Assert.Equal(token, TagInferrer.NormalizeTag(token));
    }

    [Fact]
    public void InferDoesNotTagFragmentsOfCommaGroupedNumbers()
    {
        var md = "# Chunking Is A Latency Decision\n\n" +
                 "A 4,000-character chunk with 50% overlap compares against 1.5x more vectors " +
                 "than a 2,000-character chunk, and the Azure retrieval path pays for it.";

        var tags = TagInferrer.Infer(md, ["qwen38-27b"]);

        Assert.DoesNotContain("000-character", tags);
        Assert.DoesNotContain(tags, t => t.StartsWith("000"));
    }

    [Fact]
    public void NormalizeTagFormatsCorrectly()
    {
        Assert.Equal("azure", TagInferrer.NormalizeTag("Azure"));
        Assert.Equal(".net", TagInferrer.NormalizeTag(".NET"));
    }
}
