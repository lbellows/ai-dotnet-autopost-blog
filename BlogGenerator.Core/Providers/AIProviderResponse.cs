namespace BlogGenerator.Core.Providers;

/// <summary>
/// A generated post plus every model that contributed to it, in pipeline order. Single-stage
/// providers report one model; Venice's brain/writer pair reports the research models it
/// actually reached followed by the writer that composed the post.
/// </summary>
public sealed record AIProviderResponse(string Markdown, IReadOnlyList<string> UsedModels)
{
    public AIProviderResponse(string markdown, string usedModel) : this(markdown, [usedModel]) { }
}
