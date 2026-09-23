using System.Net;
using System.Text;
using System.Text.Json;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Providers.Anthropic;

namespace BlogGenerator.Tests;

public class AnthropicProviderTests
{
    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public JsonElement Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = JsonSerializer.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static GenerationSettings Settings(double? temperature = null) => new()
    {
        AnthropicModel = "claude-sonnet-5",
        AnthropicMaxTokens = 16000,
        AnthropicTemperature = temperature,
        MaxSearches = 3,
    };

    private static PromptContext Prompt() =>
        new(new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 19), "system prompt", "user prompt", "", [], "");

    private static (CapturingHandler Handler, Func<Task<string>> Run) Arrange(string body, double? temperature = null)
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-key");
        var handler = new CapturingHandler(body);
        var provider = new AnthropicProvider(new HttpClient(handler));
        return (handler, async () => (await provider.GeneratePostAsync(Prompt(), Settings(temperature))).Markdown);
    }

    [Fact]
    public async Task OmitsTemperatureWhenUnsetAndJoinsTextBlocks()
    {
        const string body = """
            {"stop_reason":"end_turn","content":[
              {"type":"thinking","thinking":""},
              {"type":"text","text":"# Title"},
              {"type":"server_tool_use","name":"web_search"},
              {"type":"text","text":"Body."}]}
            """;
        var (handler, run) = Arrange(body);

        Assert.Equal("# Title\nBody.", await run());
        Assert.Equal("claude-sonnet-5", handler.Request.GetProperty("model").GetString());
        Assert.Equal(16000, handler.Request.GetProperty("max_tokens").GetInt32());
        Assert.False(handler.Request.TryGetProperty("temperature", out _));
    }

    [Theory]
    [InlineData("max_tokens", "max_tokens")]
    [InlineData("refusal", "declined")]
    public async Task RejectsTruncatedOrRefusedResponses(string stopReason, string expected)
    {
        var body = $$"""{"stop_reason":"{{stopReason}}","content":[{"type":"text","text":"# Half a post"}]}""";
        var (_, run) = Arrange(body);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(run);
        Assert.Contains(expected, ex.Message);
    }
}
