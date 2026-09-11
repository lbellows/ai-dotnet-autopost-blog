using BlogGenerator.Core.Providers.Local;
using BlogGenerator.Core.Research;

namespace BlogGenerator.Tests;

public class LocalProviderTests
{
    [Theory]
    [InlineData("main:8080", "http://main:8080/v1/chat/completions")]
    [InlineData("http://main:8080", "http://main:8080/v1/chat/completions")]
    [InlineData("http://main:8080/", "http://main:8080/v1/chat/completions")]
    [InlineData("http://main:8080/v1", "http://main:8080/v1/chat/completions")]
    [InlineData("http://main:8080/v1/", "http://main:8080/v1/chat/completions")]
    [InlineData("http://main:8080/v1/chat/completions", "http://main:8080/v1/chat/completions")]
    [InlineData("https://gpu.example.com/openai/v1", "https://gpu.example.com/openai/v1/chat/completions")]
    public void ResolveEndpointNormalizesBaseUrl(string configured, string expected)
    {
        WithBaseUrl(configured, () => Assert.Equal(expected, LocalProvider.ResolveEndpoint().ToString()));
    }

    [Fact]
    public void ResolveEndpointRejectsMissingBaseUrl()
    {
        WithBaseUrl(null, () =>
        {
            var ex = Assert.Throws<InvalidOperationException>(() => LocalProvider.ResolveEndpoint());
            Assert.Contains("LOCAL_AI_BASE_URL", ex.Message);
        });
    }

    [Fact]
    public void ParseCompletionReadsContentAndFinishReason()
    {
        var body = """
            {
              "model": "gemma-26b",
              "choices": [
                { "message": { "role": "assistant", "content": "# Title\n\nBody." }, "finish_reason": "stop" }
              ]
            }
            """;

        var completion = LocalProvider.ParseCompletion(body, requestedModel: "requested");

        Assert.Equal("# Title\n\nBody.", completion.Content);
        Assert.Equal("gemma-26b", completion.Model);
        Assert.Equal("stop", completion.FinishReason);
    }

    [Fact]
    public void ParseCompletionFallsBackToRequestedModelWhenResponseOmitsIt()
    {
        var completion = LocalProvider.ParseCompletion(
            """{"choices":[{"message":{"content":"Body."}}]}""", requestedModel: "gpt-oss-20b");

        Assert.Equal("gpt-oss-20b", completion.Model);
        Assert.Equal("", completion.FinishReason);
    }

    // A bare llama.cpp server answers with the GGUF path it loaded, which would tag the post with
    // a filesystem path. The requested alias is the name a reader would recognize.
    [Fact]
    public void ParseCompletionPrefersRequestedAliasOverAModelPath()
    {
        var completion = LocalProvider.ParseCompletion(
            """{"model":"/models/gemma-4-26b-a4b-Q4_K_M.gguf","choices":[{"message":{"content":"Body."}}]}""",
            requestedModel: "gemma-26b");

        Assert.Equal("gemma-26b", completion.Model);
    }

    [Fact]
    public void ParseCompletionToleratesAResponseWithNoChoices()
    {
        var completion = LocalProvider.ParseCompletion("""{"model":"gemma-26b","choices":[]}""", "gemma-26b");

        Assert.Equal("", completion.Content);
    }

    [Fact]
    public void ParseCompletionReadsToolCalls()
    {
        var body = """
            {
              "model": "qwen38-27b",
              "choices": [
                {
                  "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                      {
                        "id": "abc123def",
                        "type": "function",
                        "function": { "name": "list_recent_posts", "arguments": "{\"query\":\"dotnet\"}" }
                      }
                    ]
                  },
                  "finish_reason": "tool_calls"
                }
              ]
            }
            """;

        var call = Assert.Single(LocalProvider.ParseCompletion(body, "qwen38-27b").ToolCalls);

        Assert.Equal("abc123def", call.Id);
        Assert.Equal("list_recent_posts", call.Name);
        Assert.Equal("""{"query":"dotnet"}""", call.Arguments);
    }

    // The OpenAI shape makes arguments a JSON string, but llama.cpp builds have been seen inlining
    // the object instead. Both have to reach the toolset as JSON text.
    [Fact]
    public void ParseCompletionAcceptsToolArgumentsSentAsAnObject()
    {
        var body = """
            {"choices":[{"message":{"tool_calls":[
              {"id":"aaaaaaaaa","function":{"name":"fetch_page","arguments":{"url":"https://example.com/a"}}}
            ]}}]}
            """;

        var call = Assert.Single(LocalProvider.ParseCompletion(body, "m").ToolCalls);

        Assert.Contains("https://example.com/a", call.Arguments);
    }

    [Fact]
    public void ParseCompletionSkipsAToolCallWithNoFunctionName()
    {
        var body = """{"choices":[{"message":{"content":"hi","tool_calls":[{"id":"x","type":"function"}]}}]}""";

        Assert.Empty(LocalProvider.ParseCompletion(body, "m").ToolCalls);
    }

    [Fact]
    public void ParseCompletionReturnsNoToolCallsForAnOrdinaryAnswer()
    {
        Assert.Empty(LocalProvider
            .ParseCompletion("""{"choices":[{"message":{"content":"Body."}}]}""", "m")
            .ToolCalls);
    }

    [Fact]
    public void ReadDossierReturnsFileContent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "## In-window findings\n\nAzure shipped something.\n");
            Assert.Contains("Azure shipped something.", LocalProvider.ReadDossier(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // No dossier is a supported mode, not an error: the stand-in routes the writer prompt's
    // "In-window findings is empty" branch to evergreen mode with no links to invent.
    [Fact]
    public void ReadDossierFallsBackToTheEmptyDossierWhenNoPathIsGiven()
    {
        Assert.Equal(LocalProvider.EmptyDossier, LocalProvider.ReadDossier(null));
        Assert.Equal(LocalProvider.EmptyDossier, LocalProvider.ReadDossier("   "));
        Assert.Contains("## In-window findings", LocalProvider.EmptyDossier);
    }

    [Fact]
    public void ReadDossierRejectsAMissingFile()
    {
        Assert.Throws<FileNotFoundException>(
            () => LocalProvider.ReadDossier(Path.Combine(Path.GetTempPath(), "no-such-dossier.md")));
    }

    [Fact]
    public void ReadDossierRejectsAnEmptyFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => LocalProvider.ReadDossier(path));
            Assert.Contains("empty", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Environment variables are process-wide, so these tests must restore what they found.
    private static void WithBaseUrl(string? value, Action assert)
    {
        var original = Environment.GetEnvironmentVariable("LOCAL_AI_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("LOCAL_AI_BASE_URL", value);
            assert();
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCAL_AI_BASE_URL", original);
        }
    }
}
