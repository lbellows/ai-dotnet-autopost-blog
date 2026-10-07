using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Research;

namespace BlogGenerator.Core.Providers.Venice;

/// <summary>
/// Venice.ai provider. Venice exposes an OpenAI-compatible chat-completions endpoint plus a
/// provider-side web search toggled through <c>venice_parameters</c>, so grounding happens
/// without a client-side tool loop.
///
/// Because that search is a single retrieval pass rather than an agentic loop, this provider
/// splits generation in two: a "brain" model runs one grounded research call per angle
/// (see <see cref="PromptBuilder.ResearchAngles"/>) and a "writer" model composes the post
/// from the merged dossier with search off.
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

        var research = await ResearchAsync(promptContext, settings, brainCandidates, apiKey, ct);

        // Saved before the writer runs, so a failed or wrong post can still be checked against it.
        var savedTo = await DossierArchive.SaveAsync(research.Dossier, promptContext.Today, ct);
        Console.WriteLine($"Venice: dossier ({research.Dossier.Length:N0} chars) saved to {savedTo}");

        var completion = await CompleteAsync(
            writerCandidates,
            [
                ChatCompletions.Message("system", PromptBuilder.WriterSystemPrompt(promptContext, settings)),
                ChatCompletions.Message("user", PromptBuilder.WriterUserPrompt(promptContext, research.Dossier)),
            ],
            settings.VeniceMaxTokens,
            settings.VeniceTemperature,
            settings.VeniceTopP,
            research: false,
            apiKey,
            ct);

        var markdown = CleanModelText(completion.Content);
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidOperationException($"Venice model {completion.Model} returned no article text.");

        // The post is the work of both halves, so both are reported: the research models ground it
        // and the writer composes it, and each earns a tag on the published post.
        var usedModels = research.Models
            .Append(completion.Model)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine(
            $"Venice: wrote post with {completion.Model}; research by {string.Join(", ", research.Models)}.");
        return new AIProviderResponse(markdown, usedModels);
    }

    private async Task<(string Dossier, IReadOnlyList<string> Models)> ResearchAsync(
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
            settings, promptContext.Today, promptContext.RecentStartDate, promptContext.RecentPosts);

        var notes = new List<string>();
        var models = new List<string>();
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
                    [ChatCompletions.Message("system", researchSystem), ChatCompletions.Message("user", angles[i])],
                    settings.VeniceResearchMaxTokens,
                    settings.VeniceResearchTemperature,
                    settings.VeniceTopP,
                    research: true,
                    apiKey,
                    ct);

                var note = CleanModelText(result.Content);
                if (!string.IsNullOrWhiteSpace(note))
                {
                    notes.Add($"### Research pass {i + 1} (via {result.Model})\n\n{note}");
                    // Only a pass that contributed notes counts; a model that came back empty did
                    // not ground the post and should not be credited on it.
                    if (!models.Contains(result.Model, StringComparer.OrdinalIgnoreCase))
                        models.Add(result.Model);
                }

                foreach (var citation in ReadCitations(result.Json))
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
        return (BuildDossier(notes, citations), models);
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

    /// <summary>
    /// One chat completion, trying each model in turn. A <paramref name="research"/> call searches
    /// the web and may think; a writing call does neither, because the research is already done:
    /// with thinking left on, deepseek-v4-1-flash spent all of VeniceMaxTokens reasoning and
    /// returned no post.
    /// </summary>
    private async Task<ChatCompletion> CompleteAsync(
        IReadOnlyList<string> modelCandidates,
        IReadOnlyList<object> messages,
        int maxTokens,
        double? temperature,
        double? topP,
        bool research,
        string apiKey,
        CancellationToken ct)
    {
        Exception? lastErr = null;

        foreach (var model in modelCandidates)
        {
            try
            {
                var veniceParameters = new Dictionary<string, object>
                {
                    ["enable_web_search"] = research ? "on" : "off",
                    ["enable_web_citations"] = research,
                    // Venice prepends its own persona unless this is off, which would fight
                    // the house style prompt.
                    ["include_venice_system_prompt"] = false,
                    // Reasoning models otherwise emit <think> blocks straight into the post.
                    ["strip_thinking_response"] = true,
                };
                if (!research)
                    veniceParameters["disable_thinking"] = true;

                var request = ChatCompletions.Request(model, messages, temperature, topP);
                request["max_completion_tokens"] = maxTokens;
                request["venice_parameters"] = veniceParameters;

                var completion = await ChatCompletions.PostAsync(httpClient, ApiUrl, apiKey, request, "Venice", ct);
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

    /// <summary>The sources Venice's web search returned, which it reports beside the reply.</summary>
    internal static IReadOnlyList<VeniceCitation> ReadCitations(JsonElement json)
    {
        var citations = new List<VeniceCitation>();
        if (!json.TryGetProperty("venice_parameters", out var veniceParams) ||
            !veniceParams.TryGetProperty("web_search_citations", out var citationArray) ||
            citationArray.ValueKind != JsonValueKind.Array)
        {
            return citations;
        }

        foreach (var entry in citationArray.EnumerateArray())
        {
            var url = ChatCompletions.ReadString(entry, "url");
            if (string.IsNullOrWhiteSpace(url))
                continue;

            citations.Add(new VeniceCitation(
                Url: url,
                Title: ChatCompletions.ReadString(entry, "title"),
                Date: ChatCompletions.ReadString(entry, "date"),
                Snippet: Truncate(
                    HtmlText.ToPlainText(ChatCompletions.ReadString(entry, "content")).ReplaceLineEndings(" "),
                    CitationSnippetLength)));
        }

        return citations;
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

        text = ModelText.StripThinkingBlocks(text);
        text = CitationMarkerRegex().Replace(text, "");
        return text.Trim();
    }

    // VENICE_API_KEY is canonical; veniceApi matches the key name used in local .env files.
    private static string ResolveApiKey() => ProviderSupport.RequireEnv(
        "VENICE_API_KEY must be set to a non-empty Venice API key.", "VENICE_API_KEY", "veniceApi");

    private static string Redact(string message, string apiKey) =>
        ProviderSupport.Redact(message, (apiKey, "VENICE_API_KEY"));

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length].TrimEnd() + "…";

    // Also eats any horizontal space in front of the marker so removing a mid-sentence
    // citation does not leave a double space behind.
    [GeneratedRegex(@"[ \t]*\^\s*\d+(?:\s*,\s*\d+)*\s*\^")]
    private static partial Regex CitationMarkerRegex();
}

internal sealed record VeniceCitation(string Url, string Title, string Date, string Snippet);
