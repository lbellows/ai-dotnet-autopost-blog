using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;

namespace BlogGenerator.Core.Providers.Venice;

/// <summary>
/// Venice.ai provider. Venice exposes an OpenAI-compatible chat-completions endpoint plus a
/// provider-side web search toggled through <c>venice_parameters</c>, so grounding happens
/// without a client-side tool loop.
///
/// Because that search is a single retrieval pass rather than an agentic loop, this provider
/// splits generation in two: a "brain" model runs one grounded research call per angle
/// (see <see cref="PromptBuilder.ResearchAngles"/>) and a "writer" model composes the post
/// from the merged dossier with search off. Clearing <c>VeniceWriterModel</c> collapses this
/// back to a single search-and-write call.
/// </summary>
public sealed partial class VeniceProvider(HttpClient httpClient) : IAIProvider
{
    private const string ApiUrl = "https://api.venice.ai/api/v1/chat/completions";

    // Cap on how much raw citation text is handed to the writer. The brain's notes already
    // carry the synthesis; these entries exist so "Further reading" URLs are verbatim-real.
    private const int MaxDossierCitations = 24;
    private const int CitationSnippetLength = 240;

    public string ProviderName => "venice";

    public async Task<AIProviderResponse> GeneratePostAsync(
        PromptContext promptContext,
        GenerationSettings settings,
        CancellationToken ct = default)
    {
        var apiKey = ResolveApiKey();
        var brainCandidates = ProviderSupport.ModelCandidates(
            settings.VeniceBrainModel, settings.VeniceBrainFallbackModels);
        var writerCandidates = ProviderSupport.ModelCandidates(
            settings.VeniceWriterModel, settings.VeniceWriterFallbackModels);

        if (brainCandidates.Count == 0)
            throw new InvalidOperationException("Generation:VeniceBrainModel must name at least one Venice model.");

        // No writer configured: one grounded call both searches and writes.
        if (writerCandidates.Count == 0)
        {
            Console.WriteLine("Venice: no writer model configured; running a single search-and-write call.");
            return await WriteAsync(
                brainCandidates,
                promptContext.SystemPrompt,
                promptContext.UserPrompt,
                webSearch: true,
                settings, apiKey, ct);
        }

        var dossier = await ResearchAsync(promptContext, settings, brainCandidates, apiKey, ct);

        var written = await WriteAsync(
            writerCandidates,
            PromptBuilder.WriterSystemPrompt(promptContext, settings),
            PromptBuilder.WriterUserPrompt(promptContext, dossier),
            webSearch: false,
            settings, apiKey, ct);

        Console.WriteLine($"Venice: wrote post with {written.UsedModel}.");
        return written;
    }

    private async Task<AIProviderResponse> WriteAsync(
        IReadOnlyList<string> candidates,
        string systemPrompt,
        string userPrompt,
        bool webSearch,
        GenerationSettings settings,
        string apiKey,
        CancellationToken ct)
    {
        var completion = await CompleteAsync(
            candidates,
            [ChatMessage("system", systemPrompt), ChatMessage("user", userPrompt)],
            settings.VeniceMaxTokens,
            settings.VeniceTemperature,
            settings.VeniceTopP,
            webSearch,
            apiKey,
            ct);

        var markdown = CleanModelText(completion.Content);
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidOperationException($"Venice model {completion.Model} returned no article text.");

        return new AIProviderResponse(markdown, completion.Model);
    }

    private async Task<string> ResearchAsync(
        PromptContext promptContext,
        GenerationSettings settings,
        IReadOnlyList<string> brainCandidates,
        string apiKey,
        CancellationToken ct)
    {
        var angles = PromptBuilder
            .ResearchAngles(settings, promptContext.Today, promptContext.RecentStartDate)
            .Take(Math.Max(1, settings.MaxSearches))
            .ToList();

        var researchSystem = PromptBuilder.ResearchSystemPrompt(
            settings, promptContext.Today, promptContext.RecentStartDate);

        var notes = new List<string>();
        var citations = new List<VeniceCitation>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Exception? lastErr = null;

        for (var i = 0; i < angles.Count; i++)
        {
            Console.WriteLine($"Venice research pass {i + 1}/{angles.Count}...");
            try
            {
                var result = await CompleteAsync(
                    brainCandidates,
                    [ChatMessage("system", researchSystem), ChatMessage("user", angles[i])],
                    settings.VeniceResearchMaxTokens,
                    settings.VeniceResearchTemperature,
                    settings.VeniceTopP,
                    webSearch: true,
                    apiKey,
                    ct);

                var note = CleanModelText(result.Content);
                if (!string.IsNullOrWhiteSpace(note))
                    notes.Add($"### Research pass {i + 1} (via {result.Model})\n\n{note}");

                foreach (var citation in result.Citations)
                {
                    if (!string.IsNullOrWhiteSpace(citation.Url) && seenUrls.Add(citation.Url))
                        citations.Add(citation);
                }
            }
            catch (Exception ex)
            {
                // One barren angle should not sink the run; the remaining passes still ground the post.
                lastErr = ex;
                Console.WriteLine($"Venice research pass {i + 1} failed: {Redact(ex.Message, apiKey)}");
            }
        }

        if (notes.Count == 0)
        {
            throw new InvalidOperationException(
                $"All {angles.Count} Venice research passes failed. Last error: {Redact(lastErr?.Message ?? "unknown", apiKey)}");
        }

        Console.WriteLine($"Venice research: {notes.Count}/{angles.Count} passes succeeded, {citations.Count} unique sources.");
        return BuildDossier(notes, citations);
    }

    internal static string BuildDossier(IReadOnlyList<string> notes, IReadOnlyList<VeniceCitation> citations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Research notes");
        sb.AppendLine();
        foreach (var note in notes)
        {
            sb.AppendLine(note);
            sb.AppendLine();
        }

        if (citations.Count == 0)
            return sb.ToString().TrimEnd();

        sb.AppendLine("## Verified source URLs");
        sb.AppendLine("Use these URLs verbatim; do not construct any other link.");
        sb.AppendLine();
        foreach (var citation in citations.Take(MaxDossierCitations))
        {
            var date = string.IsNullOrWhiteSpace(citation.Date) ? "" : $" ({citation.Date})";
            sb.AppendLine($"- {citation.Url}{date} — {citation.Title}");
            if (!string.IsNullOrWhiteSpace(citation.Snippet))
                sb.AppendLine($"  > {citation.Snippet}");
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<VeniceCompletion> CompleteAsync(
        IReadOnlyList<string> modelCandidates,
        List<Dictionary<string, object>> messages,
        int maxTokens,
        double? temperature,
        double? topP,
        bool webSearch,
        string apiKey,
        CancellationToken ct)
    {
        Exception? lastErr = null;

        foreach (var model in modelCandidates)
        {
            try
            {
                var request = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["messages"] = messages,
                    ["max_completion_tokens"] = maxTokens,
                    ["venice_parameters"] = new Dictionary<string, object>
                    {
                        ["enable_web_search"] = webSearch ? "on" : "off",
                        ["enable_web_citations"] = webSearch,
                        // Venice prepends its own persona unless this is off, which would fight
                        // the house style prompt.
                        ["include_venice_system_prompt"] = false,
                        // Reasoning models otherwise emit <think> blocks straight into the post.
                        ["strip_thinking_response"] = true,
                    },
                };

                if (temperature.HasValue)
                    request["temperature"] = temperature.Value;
                if (topP.HasValue)
                    request["top_p"] = topP.Value;

                var responseBody = await ProviderSupport.PostJsonAsync(
                    httpClient, ApiUrl, request, JsonOpts, "Venice",
                    httpRequest => httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}"),
                    ct);

                var completion = ParseCompletion(responseBody, model);
                if (string.IsNullOrWhiteSpace(completion.Content))
                    throw new InvalidOperationException($"Venice model {model} returned empty content.");

                return completion;
            }
            catch (Exception ex)
            {
                lastErr = ex;
                Console.WriteLine($"Venice call failed for {model}: {Redact(ex.Message, apiKey)}");
            }
        }

        throw new InvalidOperationException(
            $"No Venice model succeeded after trying: [{string.Join(", ", modelCandidates)}]. " +
            $"Last error: {Redact(lastErr?.Message ?? "unknown", apiKey)}");
    }

    internal static VeniceCompletion ParseCompletion(string responseBody, string requestedModel)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOpts);

        var content = "";
        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                content = contentProp.GetString() ?? "";
        }

        var citations = new List<VeniceCitation>();
        if (json.TryGetProperty("venice_parameters", out var veniceParams) &&
            veniceParams.TryGetProperty("web_search_citations", out var citationArray) &&
            citationArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in citationArray.EnumerateArray())
            {
                var url = ReadString(entry, "url");
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                citations.Add(new VeniceCitation(
                    Url: url,
                    Title: ReadString(entry, "title"),
                    Date: ReadString(entry, "date"),
                    Snippet: Truncate(StripHtml(ReadString(entry, "content")), CitationSnippetLength)));
            }
        }

        var responseModel = ReadString(json, "model");
        return new VeniceCompletion(
            content,
            string.IsNullOrEmpty(responseModel) ? requestedModel : responseModel,
            citations);
    }

    /// <summary>
    /// Removes the artifacts Venice models leave in prose: superscript citation markers such as
    /// <c>^4^</c> or <c>^1,5,8^</c>, and any thinking block that survived
    /// <c>strip_thinking_response</c>.
    /// </summary>
    internal static string CleanModelText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = ThinkingBlockRegex().Replace(text, "");
        text = CitationMarkerRegex().Replace(text, "");
        return text.Trim();
    }

    // VENICE_API_KEY is canonical; veniceApi matches the key name used in local .env files.
    private static string ResolveApiKey() => ProviderSupport.RequireEnv(
        "VENICE_API_KEY must be set to a non-empty Venice API key.", "VENICE_API_KEY", "veniceApi");

    private static string Redact(string message, string apiKey) =>
        ProviderSupport.Redact(message, (apiKey, "VENICE_API_KEY"));

    private static Dictionary<string, object> ChatMessage(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string StripHtml(string value) =>
        HtmlTagRegex().Replace(value, "").Replace("\n", " ").Trim();

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length].TrimEnd() + "…";

    // Also eats any horizontal space in front of the marker so removing a mid-sentence
    // citation does not leave a double space behind.
    [GeneratedRegex(@"[ \t]*\^\s*\d+(?:\s*,\s*\d+)*\s*\^")]
    private static partial Regex CitationMarkerRegex();

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkingBlockRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed record VeniceCompletion(string Content, string Model, IReadOnlyList<VeniceCitation> Citations);

internal sealed record VeniceCitation(string Url, string Title, string Date, string Snippet);
