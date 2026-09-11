using BlogGenerator.Core.Research;

namespace BlogGenerator.Tests;

public class ResearchLoopTests
{
    [Fact]
    public async Task ReturnsTheFirstProseTurnWhenTheModelCallsNoTools()
    {
        var chat = Scripted([Prose("## In-window findings\nNone.")]);

        var dossier = await ResearchLoop.RunAsync(chat.Turn, new FakeToolset(), "sys", "user", maxRounds: 5);

        Assert.Equal("## In-window findings\nNone.", dossier);
        Assert.Equal(1, chat.Calls);
    }

    [Fact]
    public async Task FeedsToolResultsBackAndKeepsGoing()
    {
        var toolset = new FakeToolset();
        var chat = Scripted([
            Calls(new ChatToolCall("abc123def", "list_recent_posts", """{"query":"dotnet"}""")),
            Prose("## In-window findings\n.NET 10 shipped."),
        ]);

        var dossier = await ResearchLoop.RunAsync(chat.Turn, toolset, "sys", "user", maxRounds: 5);

        Assert.Equal("## In-window findings\n.NET 10 shipped.", dossier);
        Assert.Equal(("list_recent_posts", """{"query":"dotnet"}"""), Assert.Single(toolset.Invocations));

        // The second request must carry the whole exchange: the two seed messages, the assistant's
        // tool call, and the tool result it is meant to read.
        var second = chat.Conversations[1];
        Assert.Equal(4, second.Count);
        Assert.Equal("tool", Role(second[3]));
        Assert.Equal("abc123def", Field(second[3], "tool_call_id"));
        Assert.Equal("result for list_recent_posts", Field(second[3], "content"));
    }

    [Fact]
    public async Task RunsEveryToolCallInOneTurn()
    {
        var toolset = new FakeToolset();
        var chat = Scripted([
            Calls(
                new ChatToolCall("aaaaaaaaa", "list_recent_posts", "{}"),
                new ChatToolCall("bbbbbbbbb", "fetch_page", """{"url":"https://example.com/a"}""")),
            Prose("done"),
        ]);

        await ResearchLoop.RunAsync(chat.Turn, toolset, "sys", "user", maxRounds: 5);

        Assert.Equal(2, toolset.Invocations.Count);
        Assert.Equal(5, chat.Conversations[1].Count);
    }

    // The budget is what stops a model browsing forever. When it runs out the loop must still come
    // away with a dossier, so it asks once more with the tools withdrawn.
    [Fact]
    public async Task AsksForTheDossierWithoutToolsOnceTheBudgetIsSpent()
    {
        var chat = Scripted([
            Calls(new ChatToolCall("aaaaaaaaa", "list_recent_posts", "{}")),
            Calls(new ChatToolCall("bbbbbbbbb", "list_recent_posts", "{}")),
            Prose("## In-window findings\nNone."),
        ]);

        var dossier = await ResearchLoop.RunAsync(chat.Turn, new FakeToolset(), "sys", "user", maxRounds: 2);

        Assert.Equal("## In-window findings\nNone.", dossier);
        Assert.Equal(3, chat.Calls);
        Assert.Null(chat.Tools[2]);
        Assert.NotNull(chat.Tools[0]);
    }

    [Fact]
    public async Task ThrowsWhenTheFinalTurnStillSaysNothing()
    {
        var chat = Scripted([Calls(new ChatToolCall("aaaaaaaaa", "list_recent_posts", "{}")), Prose("   ")]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ResearchLoop.RunAsync(chat.Turn, new FakeToolset(), "sys", "user", maxRounds: 1));

        Assert.Contains("without producing a dossier", ex.Message);
    }

    // A model that returns neither prose nor a tool call has stalled; one nudge is cheaper than
    // failing the run, and the round budget still bounds it.
    [Fact]
    public async Task NudgesAModelThatSaysNothingAtAll()
    {
        var chat = Scripted([Prose(""), Prose("## In-window findings\nNone.")]);

        var dossier = await ResearchLoop.RunAsync(chat.Turn, new FakeToolset(), "sys", "user", maxRounds: 3);

        Assert.Equal("## In-window findings\nNone.", dossier);
        Assert.Equal("user", Role(chat.Conversations[1][^1]));
    }

    // A Mistral chat template rejects — with an HTTP 400 — any tool-call id that is not exactly
    // nine alphanumeric characters, so a server that sent none gets a conforming substitute.
    [Fact]
    public void SubstitutesAConformingIdWhenTheServerSendsNone()
    {
        var generated = ResearchLoop.CallId(new ChatToolCall("", "list_recent_posts", "{}"));

        Assert.Equal(9, generated.Length);
        Assert.All(generated, character => Assert.True(char.IsLetterOrDigit(character)));
    }

    [Fact]
    public void KeepsTheServersOwnToolCallId()
    {
        Assert.Equal("abc123def", ResearchLoop.CallId(new ChatToolCall("abc123def", "x", "{}")));
        Assert.Equal("call_0", ResearchLoop.CallId(new ChatToolCall("call_0", "x", "{}")));
    }

    private static ChatTurn Prose(string content) => new(content, []);

    private static ChatTurn Calls(params ChatToolCall[] calls) => new("", calls);

    private static string Role(object message) => Field(message, "role");

    private static string Field(object message, string key) =>
        (string)((Dictionary<string, object>)message)[key];

    private static ScriptedChat Scripted(IReadOnlyList<ChatTurn> turns) => new(turns);

    private sealed class ScriptedChat(IReadOnlyList<ChatTurn> turns)
    {
        public int Calls { get; private set; }

        public List<List<object>> Conversations { get; } = [];

        public List<IReadOnlyList<object>?> Tools { get; } = [];

        public Task<ChatTurn> Turn(
            IReadOnlyList<object> messages, IReadOnlyList<object>? tools, CancellationToken ct)
        {
            Conversations.Add([.. messages]);
            Tools.Add(tools);
            return Task.FromResult(turns[Calls++]);
        }
    }

    private sealed class FakeToolset : IResearchToolset
    {
        public List<(string Name, string Arguments)> Invocations { get; } = [];

        public IReadOnlyList<object> Schemas => [new Dictionary<string, object> { ["type"] = "function" }];

        public Task<string> InvokeAsync(string name, string argumentsJson, CancellationToken ct = default)
        {
            Invocations.Add((name, argumentsJson));
            return Task.FromResult($"result for {name}");
        }
    }
}
