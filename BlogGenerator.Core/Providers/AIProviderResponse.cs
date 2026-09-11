namespace BlogGenerator.Core.Providers;

/// <summary>
/// A generated post plus every model that contributed to it, in pipeline order. Single-stage
/// providers report one model; Venice's brain/writer pair reports the research models it
/// actually reached followed by the writer that composed the post.
/// </summary>
/// <param name="ExtraTags">
/// Provenance tags the provider wants on the post that are not model names — the local provider
/// adds <c>local</c>, because "gemma-26b" alone does not say the post was written off-cloud.
/// They are kept when tags are trimmed, exactly as model tags are.
/// </param>
public sealed record AIProviderResponse(
    string Markdown,
    IReadOnlyList<string> UsedModels,
    IReadOnlyList<string>? ExtraTags = null)
{
    public AIProviderResponse(string markdown, string usedModel) : this(markdown, [usedModel]) { }
}
