using System.Text.RegularExpressions;

namespace BlogGenerator.Core.Providers;

/// <summary>
/// Cleanup that applies to any model's prose. Reasoning models emit a thinking block before the
/// article, and whether it survives depends on the server: hosted APIs usually strip it, a local
/// llama.cpp build hands it back inline.
/// </summary>
internal static partial class ModelText
{
    /// <summary>Removes complete &lt;think&gt;...&lt;/think&gt; blocks, leaving the article behind.</summary>
    public static string StripThinkingBlocks(string text) =>
        string.IsNullOrWhiteSpace(text) ? "" : ThinkingBlockRegex().Replace(text, "").Trim();

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkingBlockRegex();
}
