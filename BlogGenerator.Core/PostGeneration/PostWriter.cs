using System.Text;
using System.Text.RegularExpressions;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Memes;
using Slugify;

namespace BlogGenerator.Core.PostGeneration;

public static partial class PostWriter
{
    [GeneratedRegex(@"^\s*#")]
    private static partial Regex HeadingRegex();

    private static readonly TimeZoneInfo EasternTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static (string FilePath, string? MemeRelPath) WritePost(
        string markdownBody,
        GenerationSettings settings,
        string? usedModel = null,
        ImgflipClient? imgflipClient = null)
    {
        markdownBody = StripLeadingInstructions(markdownBody);

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

        var summary = MemeExtractor.ExtractTldrLine(markdownBody);
        string? memeRelPath = null;

        if (settings.ImgflipMemeEnabled && imgflipClient is not null)
        {
            var hint = MemeExtractor.ExtractImgflipHint(markdownBody);
            markdownBody = MemeExtractor.RemoveImgflipHint(markdownBody);
            if (hint is not null)
            {
                var memeUrl = GenerateImgflipMemeAsync(imgflipClient, hint).GetAwaiter().GetResult();
                if (memeUrl is not null)
                {
                    memeRelPath = memeUrl;
                    markdownBody = MemeInjector.InjectMemeIntoMarkdown(markdownBody, memeUrl, title, summary);
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

        File.WriteAllText(postPath, sb.ToString(), Encoding.UTF8);
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
