namespace BlogGenerator.Core.Research;

/// <summary>
/// The tools a research stage may call, in OpenAI chat-completions shape.
/// </summary>
public interface IResearchToolset
{
    /// <summary>Tool definitions to send as the request's <c>tools</c> array.</summary>
    IReadOnlyList<object> Schemas { get; }

    /// <summary>
    /// Runs one tool call and returns what should be handed back as the tool message. A tool that
    /// cannot answer returns an explanation for the model to read rather than throwing: a failed
    /// fetch is a normal event mid-research, and the model can route around it.
    /// </summary>
    Task<string> InvokeAsync(string name, string argumentsJson, CancellationToken ct = default);
}
