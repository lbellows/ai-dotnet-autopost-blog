using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.PostGeneration;

namespace BlogGenerator.Tests;

public class CodeSampleLinterTests
{
    private static GenerationSettings CreateSettings() => new()
    {
        CodeSamplesEnabled = true,
        CodeSampleMinLines = 15,
        CodeSampleMaxLines = 30,
    };

    private static string Fenced(string language, params string[] lines) =>
        $"Intro prose.\n\n```{language}\n{string.Join("\n", lines)}\n```\n\nMore prose.";

    // A sample modelled on the good blocks the generator already produces.
    private static string[] RealisticSample() =>
    [
        "using Azure.AI.OpenAI;",
        "using Azure.Identity;",
        "",
        "var endpoint = Environment.GetEnvironmentVariable(\"AZURE_OPENAI_ENDPOINT\")",
        "    ?? throw new InvalidOperationException(\"AZURE_OPENAI_ENDPOINT is required\");",
        "",
        "var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());",
        "var chat = client.GetChatClient(\"gpt-4.1\");",
        "",
        "try",
        "{",
        "    var completion = await chat.CompleteChatAsync(\"Summarize this release.\");",
        "    Console.WriteLine(completion.Value.Content[0].Text);",
        "}",
        "catch (RequestFailedException ex) when (ex.Status == 429)",
        "{",
        "    Console.WriteLine(\"Throttled; back off and retry.\");",
        "}",
    ];

    [Fact]
    public void CleanSampleProducesNoWarnings()
    {
        var warnings = CodeSampleLinter.Inspect(Fenced("csharp", RealisticSample()), CreateSettings());
        Assert.Empty(warnings);
    }

    [Fact]
    public void PostWithoutCodeProducesNoWarnings()
    {
        var warnings = CodeSampleLinter.Inspect("Just prose, no fences at all.", CreateSettings());
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("// Pseudocode: route by intent")]
    [InlineData("// Pseudo-code sketch")]
    [InlineData("// Conceptual sketch: route, then fail over")]
    [InlineData("// Illustrative shape only")]
    [InlineData("# Conceptual flow, not a production-ready script")]
    public void DisclaimerIsFlagged(string disclaimer)
    {
        var body = RealisticSample().Prepend(disclaimer).ToArray();
        var warnings = CodeSampleLinter.Inspect(Fenced("csharp", body), CreateSettings());
        Assert.Contains(warnings, w => w.Contains("pseudocode/conceptual/illustrative"));
    }

    [Theory]
    [InlineData("    throw new NotImplementedException();")]
    [InlineData("var client = new RoutingChatClient(...);")]
    [InlineData("// ...")]
    public void StubIsFlagged(string stub)
    {
        var body = RealisticSample().Append(stub).ToArray();
        var warnings = CodeSampleLinter.Inspect(Fenced("csharp", body), CreateSettings());
        Assert.Contains(warnings, w => w.Contains("stub or '...' placeholder"));
    }

    [Fact]
    public void InstallOnlyBlockIsFlagged()
    {
        var markdown = Fenced(
            "bash",
            "# Add the packages",
            "dotnet add package Microsoft.Agents.AI",
            "dotnet add package Microsoft.Extensions.AI");

        var warnings = CodeSampleLinter.Inspect(markdown, CreateSettings());
        Assert.Contains(warnings, w => w.Contains("package-install or CLI-invocation"));
        // The short-block warning would be redundant noise on top of it.
        Assert.DoesNotContain(warnings, w => w.Contains("below the"));
    }

    [Fact]
    public void ShortBlockIsFlaggedAgainstTheConfiguredMinimum()
    {
        var markdown = Fenced("csharp", "var model = \"gpt-4.1\";", "Console.WriteLine(model);");
        var warnings = CodeSampleLinter.Inspect(markdown, CreateSettings());
        Assert.Contains(warnings, w => w.Contains("2 lines, below the 15-line minimum"));
    }

    [Fact]
    public void OverlongBlockIsFlaggedAgainstTheConfiguredMaximum()
    {
        var body = Enumerable.Repeat("Console.WriteLine(\"line\");", 31).ToArray();
        var warnings = CodeSampleLinter.Inspect(Fenced("csharp", body), CreateSettings());
        Assert.Contains(warnings, w => w.Contains("31 lines, above the 30-line maximum"));
    }

    [Fact]
    public void MultipleBlocksAreFlagged()
    {
        var markdown = Fenced("csharp", RealisticSample()) + "\n" + Fenced("csharp", RealisticSample());
        var warnings = CodeSampleLinter.Inspect(markdown, CreateSettings());
        Assert.Contains(warnings, w => w.Contains("2 code blocks"));
    }

    [Fact]
    public void CodeIsFlaggedWhenSamplesAreDisabled()
    {
        var settings = CreateSettings();
        settings.CodeSamplesEnabled = false;
        var warnings = CodeSampleLinter.Inspect(Fenced("csharp", RealisticSample()), settings);
        Assert.Contains(warnings, w => w.Contains("CodeSamplesEnabled is false"));
    }

    [Fact]
    public void ShortBlockIsNotFlaggedWhenSamplesAreDisabled()
    {
        var settings = CreateSettings();
        settings.CodeSamplesEnabled = false;
        var warnings = CodeSampleLinter.Inspect(Fenced("csharp", "var x = 1;"), settings);
        Assert.DoesNotContain(warnings, w => w.Contains("below the"));
    }

    [Fact]
    public void UnterminatedFenceIsStillInspected()
    {
        var warnings = CodeSampleLinter.Inspect(
            "Intro.\n\n```csharp\n// Pseudocode: unfinished\nvar x = 1;", CreateSettings());
        Assert.Contains(warnings, w => w.Contains("pseudocode/conceptual/illustrative"));
    }
}
