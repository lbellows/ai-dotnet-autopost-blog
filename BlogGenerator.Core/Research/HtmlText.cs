using System.Net;
using System.Text.RegularExpressions;

namespace BlogGenerator.Core.Research;

/// <summary>
/// Turns HTML into the plain text a model should read. Feed summaries arrive as HTML fragments and
/// article pages as whole documents, and in both cases the markup is noise that would only burn
/// context. This is deliberately a regex scrubber rather than a parser: the output is prose for a
/// model to read, never markup to re-render, so being approximately right costs nothing and keeps
/// the project free of an HTML-parsing dependency.
/// </summary>
internal static partial class HtmlText
{
    /// <summary>
    /// Strips markup, decodes entities, and collapses whitespace.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";

        // Script and style bodies are not prose, and dropping them before the generic tag strip is
        // what stops a page's JavaScript from arriving as a wall of text.
        var text = DroppedElementRegex().Replace(html, " ");
        text = CommentRegex().Replace(text, " ");

        // Whitespace in the source is insignificant in HTML, so flatten it before anything else:
        // otherwise a publisher who hard-wraps their markup gets line breaks mid-sentence. The
        // cost is that a <pre> block's own line structure is lost, which is the right trade for
        // notes a model will read as prose.
        text = WhitespaceRegex().Replace(text, " ");

        // Structure then comes only from the markup: a paragraph or list-item boundary becomes a
        // line break so headings and bullets do not run into the sentence after them.
        text = BlockBoundaryRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);

        // Collapse runs of spaces, then runs of blank lines, so the result reads as paragraphs.
        text = HorizontalSpaceRegex().Replace(text, " ");
        text = BlankLineRegex().Replace(text, "\n\n");

        return string.Join("\n", text.Split('\n').Select(line => line.Trim())).Trim();
    }

    /// <summary>
    /// Shortens <paramref name="text"/> to <paramref name="maxChars"/>, cutting at a word boundary
    /// and saying so, so a model never mistakes a truncated page for the whole one.
    /// </summary>
    public static string Truncate(string text, int maxChars)
    {
        if (maxChars <= 0 || text.Length <= maxChars)
            return text;

        var cut = text.LastIndexOf(' ', Math.Min(maxChars, text.Length - 1));
        if (cut < maxChars / 2)
            cut = maxChars;

        return $"{text[..cut].TrimEnd()}\n\n[truncated after {cut:N0} characters]";
    }

    [GeneratedRegex(@"<(script|style|noscript|svg)\b[^>]*>.*?</\1\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex DroppedElementRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"</?(p|div|br|li|tr|h[1-6]|section|article|header|footer|blockquote|pre)\b[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex HorizontalSpaceRegex();

    [GeneratedRegex(@"\n\s*\n\s*(\n\s*)+")]
    private static partial Regex BlankLineRegex();
}
