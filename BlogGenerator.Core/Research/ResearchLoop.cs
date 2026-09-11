namespace BlogGenerator.Core.Research;

/// <summary>One tool call the model asked for.</summary>
public sealed record ChatToolCall(string Id, string Name, string Arguments);

/// <summary>One assistant turn: prose, tool calls, or both.</summary>
public sealed record ChatTurn(string Content, IReadOnlyList<ChatToolCall> ToolCalls);

/// <summary>
/// Sends a conversation and returns the assistant's next turn. Passing <c>null</c> for
/// <paramref name="tools"/> means "answer in prose; no tools this time".
/// </summary>
public delegate Task<ChatTurn> ChatTurnAsync(
    IReadOnlyList<object> messages,
    IReadOnlyList<object>? tools,
    CancellationToken ct);

/// <summary>
/// Drives a model through a bounded tool-calling loop and returns the research dossier it writes.
///
/// This is the piece the hosted providers do not need: Anthropic and Venice run search on their own
/// side, so the whole retrieval loop happens inside one API call. A self-hosted server answers plain
/// chat completions, so the loop has to live here — which is also why it is provider-neutral rather
/// than buried in one provider.
/// </summary>
public static class ResearchLoop
{
    public static async Task<string> RunAsync(
        ChatTurnAsync chat,
        IResearchToolset toolset,
        string systemPrompt,
        string userPrompt,
        int maxRounds,
        Action<string>? log = null,
        CancellationToken ct = default)
    {
        var messages = new List<object>
        {
            TextMessage("system", systemPrompt),
            TextMessage("user", userPrompt),
        };

        for (var round = 1; round <= maxRounds; round++)
        {
            var turn = await chat(messages, toolset.Schemas, ct);

            if (turn.ToolCalls.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(turn.Content))
                    return turn.Content.Trim();

                // Said nothing and asked for nothing. Spending a round on an explicit nudge beats
                // failing the run, and the round budget still bounds it.
                messages.Add(TextMessage("user",
                    "You produced no output. Call a tool to gather sources, or write the dossier now."));
                continue;
            }

            messages.Add(ToolCallMessage(turn));

            foreach (var call in turn.ToolCalls)
            {
                var result = await toolset.InvokeAsync(call.Name, call.Arguments, ct);
                log?.Invoke($"  round {round}: {call.Name}({Summarize(call.Arguments)}) -> {result.Length:N0} chars");
                messages.Add(ToolResultMessage(call, result));
            }
        }

        // Out of rounds. Ask for the write-up with the tools withdrawn, so a model that would
        // happily keep browsing has to produce the dossier from what it already read.
        log?.Invoke($"  tool budget of {maxRounds} round(s) spent; asking for the dossier now.");
        messages.Add(TextMessage("user",
            "You have used your research budget — no more tool calls are available. Write the dossier now, " +
            "using only what the tools already returned."));

        var final = await chat(messages, null, ct);
        if (string.IsNullOrWhiteSpace(final.Content))
            throw new InvalidOperationException("The research stage ended without producing a dossier.");

        return final.Content.Trim();
    }

    private static Dictionary<string, object> TextMessage(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private static Dictionary<string, object> ToolCallMessage(ChatTurn turn) =>
        new()
        {
            ["role"] = "assistant",
            // Empty rather than absent: some chat templates index content unconditionally.
            ["content"] = turn.Content,
            ["tool_calls"] = turn.ToolCalls
                .Select(call => (object)new Dictionary<string, object>
                {
                    ["id"] = CallId(call),
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = call.Name,
                        ["arguments"] = call.Arguments,
                    },
                })
                .ToList(),
        };

    private static Dictionary<string, object> ToolResultMessage(ChatToolCall call, string result) =>
        new()
        {
            ["role"] = "tool",
            ["tool_call_id"] = CallId(call),
            ["content"] = result,
        };

    /// <summary>
    /// The id to echo back for <paramref name="call"/>. Normally the server's own, but a Mistral
    /// chat template rejects — with an HTTP 400, not a soft failure — any id that is not exactly
    /// nine alphanumeric characters, so a server that omitted one gets a conforming substitute
    /// rather than an empty string.
    /// </summary>
    internal static string CallId(ChatToolCall call) =>
        call.Id.Length == 9 && call.Id.All(char.IsLetterOrDigit)
            ? call.Id
            : call.Id.Length > 0
                ? call.Id
                : GenerateCallId();

    private static string GenerateCallId() =>
        string.Concat(Enumerable.Range(0, 9).Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]));

    private static string Summarize(string arguments) =>
        arguments.Length <= 120 ? arguments.ReplaceLineEndings(" ") : arguments[..120].ReplaceLineEndings(" ") + "…";

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
}
