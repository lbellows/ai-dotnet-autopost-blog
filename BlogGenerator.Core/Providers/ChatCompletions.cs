using System.Text.Json;
using System.Text.Json.Serialization;
using BlogGenerator.Core.Research;

namespace BlogGenerator.Core.Providers;

/// <summary>
/// One reply from an OpenAI-compatible chat-completions endpoint.
/// </summary>
/// <param name="Json">The whole response, for fields only one server sends (Venice's citations).</param>
internal sealed record ChatCompletion(
    string Content,
    string Model,
    string FinishReason,
    IReadOnlyList<ChatToolCall> ToolCalls,
    JsonElement Json);

/// <summary>
/// The OpenAI chat-completions wire format, which Venice and every local server (llama.cpp,
/// llama-swap, Ollama, vLLM) speak. Each provider adds its own fields to the request — Venice its
/// <c>venice_parameters</c>, the local server its tools — and reads the shared reply from here.
/// </summary>
internal static class ChatCompletions
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Dictionary<string, object> Message(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    /// <summary>
    /// The request fields every server shares. Sampling parameters are only sent when set, so a
    /// null leaves the server's own default in place.
    /// </summary>
    public static Dictionary<string, object> Request(
        string model, IReadOnlyList<object> messages, double? temperature, double? topP)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages,
        };

        if (temperature.HasValue)
            request["temperature"] = temperature.Value;
        if (topP.HasValue)
            request["top_p"] = topP.Value;

        return request;
    }

    /// <summary>
    /// POSTs <paramref name="request"/> and parses the reply. <paramref name="apiKey"/> is optional
    /// because a local server on a trusted LAN usually runs open.
    /// </summary>
    public static async Task<ChatCompletion> PostAsync(
        HttpClient http,
        string url,
        string? apiKey,
        Dictionary<string, object> request,
        string providerName,
        CancellationToken ct)
    {
        var body = await ProviderSupport.PostJsonAsync(
            http, url, request, JsonOptions, providerName,
            httpRequest =>
            {
                if (!string.IsNullOrEmpty(apiKey))
                    httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            },
            ct);

        return Parse(body, (string)request["model"]);
    }

    public static ChatCompletion Parse(string responseBody, string requestedModel)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(responseBody, JsonOptions);

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

        var responseModel = ReadString(json, "model");
        return new ChatCompletion(
            content,
            responseModel.Length == 0 ? requestedModel : responseModel,
            finishReason,
            toolCalls,
            json);
    }

    /// <summary>The string value of <paramref name="property"/>, or empty when absent or not a string.</summary>
    public static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

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
}
