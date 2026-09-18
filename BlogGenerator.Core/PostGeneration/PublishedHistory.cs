using System.Text.RegularExpressions;

namespace BlogGenerator.Core.PostGeneration;

/// <summary>One already-published post, as the prompt needs to describe it.</summary>
public sealed record PublishedPost(DateOnly Date, string Title);

/// <summary>
/// Reads what this blog has already published out of <c>_posts/</c>.
///
/// The generator is otherwise stateless across runs: every run researches the same fixed angles
/// and neither stage can see the archive, so when the freshness window is thin both the research
/// and the writing collapse onto whichever evergreen material ranks highest — five of the nine
/// posts before this existed were Azure OpenAI cost pieces built on the same four pages. The
/// prompts can only avoid repeating a post if something tells them what the last few were.
/// </summary>
public static partial class PublishedHistory
{
    // The date a post was published is its filename prefix — Jekyll requires YYYY-MM-DD-slug.md,
    // so it is more reliable than the front matter's timestamp and sorts lexically.
    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})-")]
    private static partial Regex FilenameDateRegex();

    [GeneratedRegex(@"^title:\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex FrontMatterTitleRegex();

    /// <summary>
    /// The most recent <paramref name="count"/> posts, newest first. Returns an empty list when
    /// there is no archive yet: a first run on a fresh checkout has nothing to avoid repeating,
    /// which is not an error.
    /// </summary>
    public static IReadOnlyList<PublishedPost> Read(string repoRoot, int count)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(repoRoot))
            return [];

        var postsDir = Path.Combine(repoRoot, "_posts");
        if (!Directory.Exists(postsDir))
            return [];

        return Directory.EnumerateFiles(postsDir, "*.md")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Select(ReadPost)
            .Where(post => post is not null)
            .Take(count)
            .ToList()!;
    }

    private static PublishedPost? ReadPost(string path)
    {
        var name = Path.GetFileName(path);
        var dateMatch = FilenameDateRegex().Match(name);
        if (!dateMatch.Success ||
            !DateOnly.TryParse(dateMatch.Value.TrimEnd('-'), out var date))
        {
            return null;
        }

        string title;
        try
        {
            title = ReadTitle(path);
        }
        catch (IOException)
        {
            // A post we cannot read is one topic we fail to exclude, not a reason to abandon a run.
            return null;
        }

        if (string.IsNullOrWhiteSpace(title))
            title = SlugToTitle(name[dateMatch.Length..]);

        return new PublishedPost(date, title);
    }

    private static string ReadTitle(string path)
    {
        // Only the front matter is needed, and posts run to a few thousand words.
        var head = string.Join("\n", File.ReadLines(path).Take(12));
        var match = FrontMatterTitleRegex().Match(head);
        return match.Success ? UnescapeYaml(match.Groups[1].Value) : string.Empty;
    }

    // The inverse of PostWriter.EscapeYamlString, for the double-quoted form it always writes.
    private static string UnescapeYaml(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
            value = value[1..^1];

        return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    // Fallback for a hand-written post with no front matter title: the slug still names the topic.
    private static string SlugToTitle(string fileName)
    {
        var slug = Path.GetFileNameWithoutExtension(fileName).Replace('-', ' ').Trim();
        return slug.Length == 0 ? string.Empty : string.Concat(char.ToUpperInvariant(slug[0]), slug[1..]);
    }
}
