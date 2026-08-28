using System.Text.Json;
using System.Text.Json.Serialization;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;

namespace BlogGenerator.Core.Providers.Anthropic;

public sealed class AnthropicProvider(HttpClient httpClient) : IAIProvider
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public string ProviderName => "anthropic";

    public async Task<AIProviderResponse> GeneratePostAsync(
        PromptContext promptContext,
        GenerationSettings settings,
        CancellationToken ct = default)
    {
        var apiKey = ProviderSupport.RequireEnv(
            "ANTHROPIC_API_KEY must be set for Anthropic client", "ANTHROPIC_API_KEY");

        var request = new Dictionary<string, object>
        {
            ["model"] = settings.AnthropicModel,
            ["max_tokens"] = settings.AnthropicMaxTokens,
            ["system"] = promptContext.SystemPrompt,
            ["messages"] = new[]
            {
                new { role = "user", content = promptContext.UserPrompt }
            },
            ["tools"] = BuildTools(settings),
        };

        if (settings.AnthropicTemperature.HasValue)
            request["temperature"] = settings.AnthropicTemperature.Value;

        var responseBody = await ProviderSupport.PostJsonAsync(
            httpClient, ApiUrl, request, JsonOpts, "Anthropic",
            httpRequest =>
            {
                httpRequest.Headers.Add("x-api-key", apiKey);
                httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            },
            ct);

        var markdown = ExtractText(responseBody);
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidOperationException("Anthropic response did not contain text content");

        return new AIProviderResponse(markdown, settings.AnthropicModel);
    }

    // The response is a list of content blocks; the search results and tool calls are interleaved
    // with the prose, so only the text blocks make up the article.
    private static string ExtractText(string responseBody)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOpts);
        if (!json.TryGetProperty("content", out var contentArray))
            return string.Empty;

        var parts = contentArray.EnumerateArray()
            .Where(block =>
                block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                block.TryGetProperty("text", out _))
            .Select(block => block.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim());

        return string.Join("\n", parts);
    }

    // Domains arrive already trimmed, lower-cased, and de-duplicated from GenerationSettings.Normalize().
    private static List<object> BuildTools(GenerationSettings settings)
    {
        var toolDef = new Dictionary<string, object>
        {
            ["type"] = "web_search_20250305",
            ["name"] = "web_search",
            ["max_uses"] = settings.MaxSearches,
        };

        if (settings.AllowedDomains.Count > 0)
            toolDef["allowed_domains"] = settings.AllowedDomains;
        if (settings.BlockedDomains.Count > 0)
            toolDef["blocked_domains"] = settings.BlockedDomains;

        return [toolDef];
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
