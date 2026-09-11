using System.Text.Json;
using System.Text.Json.Serialization;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Research;

namespace BlogGenerator.Core.Providers.Local;

/// <summary>
/// A self-hosted OpenAI-compatible chat-completions server (llama.cpp, llama-swap, Ollama, vLLM).
///
/// Deliberately single-model. The hosted providers can afford Venice's brain/writer split because
/// both models sit behind an API; a local box has one GPU pool, so a second model means either
/// evicting the first or not fitting at all. Research and writing are therefore two passes of the
/// SAME model in the same run — no swap, no second load.
///
/// There is no provider-side web search here either, so the research pass goes through
/// <see cref="FeedResearchTools"/>: publisher feeds and article fetches, no search API and no key.
/// It produces a dossier, and the writing pass composes from that dossier alone using the same
/// writer prompts as the Venice writer stage, which forbid any URL the dossier does not contain.
/// With no dossier at all the model is told so plainly, which routes it to evergreen mode with no
/// sources rather than invented ones.
/// </summary>
public sealed class LocalProvider(HttpClient httpClient, FeedResearchTools researchTools) : IAIProvider
{
    // What the writer stage is handed when nobody researched anything. Shaped like a real dossier
    // so the "In-window findings is empty => evergreen, print no URLs" rules in WriterSystemPrompt
    // fire exactly as they would on a barren research run.
    internal const string EmptyDossier = """
        ## In-window findings
        None. No research was run for this post.

        ## Context
        None supplied.

        ## Sources
        None. There are no verified URLs available for this post, so print no links at all and
        omit the "Further reading" section entirely rather than listing anything from memory.
        """;

    public string ProviderName => "local";

    public async Task<AIProviderResponse> GeneratePostAsync(
        PromptContext promptContext,
        GenerationSettings settings,
        CancellationToken ct = default)
    {
        var endpoint = ResolveEndpoint();
        var model = ProviderSupport.RequireEnv(
            "LOCAL_AI_MODEL must name the model to load on the local server (see .env.example).",
            "LOCAL_AI_MODEL");
        // Optional: llama.cpp and llama-swap usually run open on a trusted LAN, vLLM and Ollama
        // behind a proxy may not.
        var apiKey = Environment.GetEnvironmentVariable("LOCAL_AI_API_KEY")?.Trim();

        Console.WriteLine($"Local: {model} at {endpoint.GetLeftPart(UriPartial.Authority)}");

        var dossier = await ResolveDossierAsync(endpoint, apiKey, model, promptContext, settings, ct);

        var completion = await CompleteAsync(
            endpoint, apiKey, model,
            [
                ChatMessage("system", PromptBuilder.WriterSystemPrompt(promptContext, settings)),
                ChatMessage("user", PromptBuilder.WriterUserPrompt(promptContext, dossier)),
            ],
            tools: null,
            maxTokens: settings.LocalMaxTokens,
            temperature: settings.LocalTemperature,
            topP: settings.LocalTopP,
            settings, ct);

        if (completion.FinishReason.Equals("length", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"Local: {completion.Model} hit the {settings.LocalMaxTokens}-token cap, so the post may be cut off " +
                "mid-sentence. Review it before publishing, or raise Generation:LocalMaxTokens.");
        }

        var markdown = ModelText.StripThinkingBlocks(completion.Content);
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new InvalidOperationException(
                $"Local model {completion.Model} returned no article text. If it is a reasoning model it may have " +
                "spent the whole token budget thinking; raise Generation:LocalMaxTokens.");
        }

        Console.WriteLine($"Local: wrote post with {completion.Model}.");

        // "local" rides alongside the model name so a post is identifiable as self-hosted even
        // though the model tag alone ("gemma-26b") does not say where it ran.
        return new AIProviderResponse(markdown, [completion.Model], ["local"]);
    }

    /// <summary>
    /// Decides where this run's facts come from: a dossier the caller already wrote, a feed
    /// research pass by the same model, or nothing at all.
    /// </summary>
    private async Task<string> ResolveDossierAsync(
        Uri endpoint,
        string? apiKey,
        string model,
        PromptContext promptContext,
        GenerationSettings settings,
        CancellationToken ct)
    {
        // An explicit --dossier outranks the feed pass: it usually came from an agent with real web
        // search, which is strictly better research than ten feeds.
        if (!string.IsNullOrWhiteSpace(settings.LocalDossierPath))
            return ReadDossier(settings.LocalDossierPath);

        if (settings.ResearchDisabled)
        {
            Console.WriteLine("Local: --no-research, so the post will be evergreen with no source links.");
            return EmptyDossier;
        }

        if (settings.ResearchFeeds.Count == 0)
        {
            Console.WriteLine(
                "Local: no Generation:ResearchFeeds are configured and no --dossier was supplied, so the post will " +
                "be evergreen with no source links.");
            return EmptyDossier;
        }

        return await ResearchAsync(endpoint, apiKey, model, promptContext, settings, ct);
    }

    private async Task<string> ResearchAsync(
        Uri endpoint,
        string? apiKey,
        string model,
        PromptContext promptContext,
        GenerationSettings settings,
        CancellationToken ct)
    {
        Console.WriteLine(
            $"Local: researching with {model} over {settings.ResearchFeeds.Count} feed(s), " +
            $"up to {settings.LocalResearchMaxRounds} round(s).");

        var toolset = researchTools.ForWindow(promptContext.Today, promptContext.RecentStartDate);

        var dossier = await ResearchLoop.RunAsync(
            chat: async (messages, tools, token) =>
            {
                var turn = await CompleteAsync(
                    endpoint, apiKey, model, messages, tools,
                    maxTokens: settings.LocalResearchMaxTokens,
                    temperature: settings.LocalResearchTemperature,
                    topP: settings.LocalTopP,
                    settings, token);

                return new ChatTurn(ModelText.StripThinkingBlocks(turn.Content), turn.ToolCalls);
            },
            toolset,
            PromptBuilder.FeedResearchSystemPrompt(settings, promptContext.Today, promptContext.RecentStartDate),
            PromptBuilder.FeedResearchUserPrompt(settings, promptContext.Today, promptContext.RecentStartDate),
            settings.LocalResearchMaxRounds,
            log: Console.WriteLine,
            ct);

        // Saved so the post can be checked against it afterwards — "is every URL in the post in the
        // dossier?" is the review question, and it needs the dossier to still exist.
        var savedTo = Path.Combine(
            Path.GetTempPath(), $"blog-dossier-{promptContext.Today:yyyy-MM-dd}-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(savedTo, dossier, ct);
        Console.WriteLine($"Local: dossier ({dossier.Length:N0} chars) saved to {savedTo}");

        return dossier;
    }

    private async Task<LocalCompletion> CompleteAsync(
        Uri endpoint,
        string? apiKey,
        string model,
        IReadOnlyList<object> messages,
        IReadOnlyList<object>? tools,
        int maxTokens,
        double? temperature,
        double? topP,
        GenerationSettings settings,
        CancellationToken ct)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["max_tokens"] = maxTokens,
            ["stream"] = false,
        };

        if (tools is { Count: > 0 })
            request["tools"] = tools;
        if (temperature.HasValue)
            request["temperature"] = temperature.Value;
        if (topP.HasValue)
            request["top_p"] = topP.Value;

        string responseBody;
        try
        {
            responseBody = await ProviderSupport.PostJsonAsync(
                httpClient, endpoint.ToString(), request, JsonOpts, "Local",
                httpRequest =>
                {
                    if (!string.IsNullOrEmpty(apiKey))
                        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
                },
                ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // A cold llama-swap has to evict the resident model and page the new weights onto the
            // GPUs before the first token, so the usual culprit is a timeout that is simply too
            // short rather than a server that is down.
            throw new InvalidOperationException(
                $"Local server at {endpoint} did not respond within {settings.LocalTimeoutMinutes} minute(s). " +
                $"Loading {model} plus generating the post can take longer on a cold server — raise " +
                "Generation:LocalTimeoutMinutes in appsettings.json.", ex);
        }

        return ParseCompletion(responseBody, model);
    }

    /// <summary>
    /// Reads the research dossier the caller gathered, or returns <see cref="EmptyDossier"/> when
    /// there is none.
    /// </summary>
    internal static string ReadDossier(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Local: no dossier supplied, so the post will be evergreen with no source links.");
            return EmptyDossier;
        }

        if (!File.Exists(path))
            throw new FileNotFoundException($"Research dossier not found: {path}", path);

        var dossier = File.ReadAllText(path).Trim();
        if (dossier.Length == 0)
            throw new InvalidOperationException($"Research dossier {path} is empty.");

        Console.WriteLine($"Local: writing from dossier {path} ({dossier.Length:N0} chars).");
        return dossier;
    }

    /// <summary>
    /// Turns <c>LOCAL_AI_BASE_URL</c> into the chat-completions URL. Accepts a bare host
    /// (<c>main:8080</c>), a root URL, or a URL that already ends in <c>/v1</c> or the full path,
    /// because which of those a server documents varies by server.
    /// </summary>
    internal static Uri ResolveEndpoint()
    {
        var raw = ProviderSupport.RequireEnv(
            "LOCAL_AI_BASE_URL must point at the local OpenAI-compatible server (see .env.example).",
            "LOCAL_AI_BASE_URL");

        // Everything here is LAN-local, so a bare host means http rather than https.
        if (!raw.Contains("://", StringComparison.Ordinal))
            raw = $"http://{raw}";

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException($"LOCAL_AI_BASE_URL is not a valid URL: {raw}");

        var path = baseUri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return baseUri;

        var suffix = path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? "chat/completions"
            : "v1/chat/completions";

        return new Uri(baseUri, $"{path}/{suffix}");
    }

    internal static LocalCompletion ParseCompletion(string responseBody, string requestedModel)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOpts);

        var content = "";
        var finishReason = "";
        IReadOnlyList<ChatToolCall> toolCalls = [];
        if (json.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message))
            {
                content = ReadString(message, "content");
                toolCalls = ReadToolCalls(message);
            }
            finishReason = ReadString(choice, "finish_reason");
        }

        // llama-swap answers with the alias that was requested, but a plain llama.cpp server
        // reports the GGUF path it loaded, which would make a useless tag.
        var responseModel = ReadString(json, "model");
        var model = responseModel.Length == 0 || responseModel.Contains('/') || responseModel.Contains('\\')
            ? requestedModel
            : responseModel;

        return new LocalCompletion(content, model, finishReason, toolCalls);
    }

    private static IReadOnlyList<ChatToolCall> ReadToolCalls(JsonElement message)
    {
        if (!message.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array)
            return [];

        var parsed = new List<ChatToolCall>();
        foreach (var call in calls.EnumerateArray())
        {
            if (!call.TryGetProperty("function", out var function))
                continue;

            var name = ReadString(function, "name");
            if (name.Length == 0)
                continue;

            // Arguments are a JSON *string* in the OpenAI shape, but some servers inline the object.
            var arguments = function.TryGetProperty("arguments", out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.GetRawText()
                : "";

            parsed.Add(new ChatToolCall(ReadString(call, "id"), name, arguments));
        }

        return parsed;
    }

    private static Dictionary<string, object> ChatMessage(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed record LocalCompletion(
    string Content,
    string Model,
    string FinishReason,
    IReadOnlyList<ChatToolCall> ToolCalls);
