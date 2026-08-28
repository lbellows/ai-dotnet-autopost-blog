using System.Text;
using System.Text.RegularExpressions;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Memes;
using Slugify;

namespace BlogGenerator.Core.PostGeneration;

public static partial class PostWriter
{
    // Matches a list-item line that is just a bullet marker with no content (e.g. "-", "* ", "+").
    [GeneratedRegex(@"^(\s*)([-*+])\s*$")]
    private static partial Regex EmptyBulletRegex();

    private static readonly TimeZoneInfo EasternTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    // Encoding.UTF8 emits a byte-order mark, which lands in front of the YAML front matter
    // delimiter. Jekyll tolerates it, but it makes the raw files awkward to diff and grep.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static (string FilePath, string? MemeRelPath) WritePost(
        string markdownBody,
        GenerationSettings settings,
        string? usedModel = null,
        ImgflipClient? imgflipClient = null)
    {
        markdownBody = StripLeadingInstructions(markdownBody);
        markdownBody = NormalizeBrokenBullets(markdownBody);

        var currentDay = DateOnly.FromDateTime(DateTime.UtcNow);
        var postsDir = Path.Combine(settings.RepoRoot, "_posts");
        Directory.CreateDirectory(postsDir);

        var title = TitleExtractor.Extract(markdownBody);
        markdownBody = StripLeadingTitleHeading(markdownBody);
        markdownBody = StripLeadingPostMetadata(markdownBody);
        var helper = new SlugHelper();
        var slug = helper.GenerateSlug(title);
        if (slug.Length > 80) slug = slug[..80];

        var existingPattern = $"{currentDay:yyyy-MM-dd}-*.md";
        if (Directory.GetFiles(postsDir, existingPattern).Length > 0)
            slug += "-2";

        string? memeRelPath = null;

        if (settings.ImgflipMemeEnabled && imgflipClient is not null)
        {
            var hint = MemeExtractor.ExtractImgflipHint(markdownBody);
            if (hint is not null)
            {
                var memeUrl = GenerateImgflipMemeAsync(imgflipClient, hint).GetAwaiter().GetResult();
                if (memeUrl is not null)
                {
                    memeRelPath = memeUrl;
                    // Replace the comment in-place so the meme appears where GPT chose to put it.
                    markdownBody = MemeExtractor.ReplaceImgflipHint(markdownBody, memeUrl, title);
                }
                else
                {
                    markdownBody = MemeExtractor.RemoveImgflipHint(markdownBody);
                }
            }
            else
            {
                Console.WriteLine("Imgflip: no meme hint found in post; skipping meme.");
            }
        }


        var postPath = Path.Combine(postsDir, $"{currentDay:yyyy-MM-dd}-{slug}.md");

        var nowNy = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTimeZone);
        var publishDt = nowNy.AddMinutes(-1);

        var modelTag = (usedModel ?? "claude").Trim();
        var mergedTags = TagInferrer.Infer(markdownBody, modelTag);

        var offset = EasternTimeZone.GetUtcOffset(nowNy);
        var offsetStr = $"{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours):D2}{offset.Minutes:D2}";

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"layout: post");
        sb.AppendLine($"title: \"{EscapeYamlString(title)}\"");
        sb.AppendLine($"date: {publishDt:yyyy-MM-dd HH:mm:ss} {offsetStr}");
        sb.AppendLine($"tags: [{string.Join(", ", mergedTags)}]");
        sb.AppendLine($"author: {settings.DefaultAuthor}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(markdownBody);

        File.WriteAllText(postPath, sb.ToString(), Utf8NoBom);
        Console.WriteLine($"Wrote {postPath}");

        return (postPath, memeRelPath);
    }

    internal static string StripLeadingInstructions(string markdownBody)
    {
        var lines = markdownBody.Split('\n');
        var cleaned = new List<string>();
        var foundHeading = false;

        foreach (var line in lines)
        {
            if (!foundHeading)
            {
                if (line.TrimStart().StartsWith('#'))
                {
                    foundHeading = true;
                    cleaned.Add(line);
                }
            }
            else
            {
                cleaned.Add(line);
            }
        }

        return foundHeading
            ? string.Join("\n", cleaned).TrimStart('\n')
            : markdownBody.Trim();
    }

    // The model sometimes emits a bullet marker alone on one line with the item text on the next
    // line, which Markdown renders as a stray "-" rather than a list. Rejoin those onto one line.
    internal static string NormalizeBrokenBullets(string markdownBody)
    {
        var lines = markdownBody.ReplaceLineEndings("\n").Split('\n');
        var result = new List<string>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var match = EmptyBulletRegex().Match(lines[i]);
            if (match.Success)
            {
                // Find the next non-blank line and attach it as the bullet's content.
                var j = i + 1;
                while (j < lines.Length && string.IsNullOrWhiteSpace(lines[j]))
                    j++;

                if (j < lines.Length)
                {
                    var indent = match.Groups[1].Value;
                    var marker = match.Groups[2].Value;
                    result.Add($"{indent}{marker} {lines[j].TrimStart()}");
                    i = j;
                    continue;
                }
            }

            result.Add(lines[i]);
        }

        return string.Join("\n", result);
    }

    internal static string StripLeadingTitleHeading(string markdownBody)
    {
        var trimmed = markdownBody.TrimStart();
        if (!trimmed.StartsWith("# "))
            return markdownBody;

        var newlineIndex = trimmed.IndexOf('\n');
        if (newlineIndex < 0)
            return string.Empty;

        return trimmed[(newlineIndex + 1)..].TrimStart('\n', '\r');
    }

    internal static string StripLeadingPostMetadata(string markdownBody)
    {
        var lines = markdownBody.ReplaceLineEndings("\n").Split('\n').ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);

        if (lines.Count == 0)
            return string.Empty;

        if (!lines[0].TrimStart().StartsWith("**Published:**", StringComparison.OrdinalIgnoreCase))
            return markdownBody;

        lines.RemoveAt(0);

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);

        if (lines.Count > 0 && lines[0].Trim() == "---")
        {
            lines.RemoveAt(0);
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
                lines.RemoveAt(0);
        }

        return string.Join("\n", lines);
    }

    private static async Task<string?> GenerateImgflipMemeAsync(ImgflipClient client, ImgflipHint hint)
    {
        try
        {
            var templates = await client.GetTemplatesAsync();
            return await client.CaptionAsync(hint, templates);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Imgflip meme generation failed: {ex.Message}");
            return null;
        }
    }

    private static string EscapeYamlString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
