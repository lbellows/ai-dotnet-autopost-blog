using System.Text.RegularExpressions;

namespace BlogGenerator.Core.Memes;

public record ImgflipHint(string TemplateName, string TopText, string BottomText);

public static partial class MemeExtractor
{
    // Matches: <!-- meme: template=drake, top="...", bottom="..." -->
    [GeneratedRegex(
        @"<!--\s*meme:\s*template=(?<template>[^,]+),\s*top=""(?<top>[^""]*)"",\s*bottom=""(?<bottom>[^""]*)""\s*-->",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImgflipHintPattern();

    public static ImgflipHint? ExtractImgflipHint(string markdownBody)
    {
        var match = ImgflipHintPattern().Match(markdownBody);
        if (!match.Success) return null;
        return new ImgflipHint(
            match.Groups["template"].Value.Trim(),
            match.Groups["top"].Value.Trim(),
            match.Groups["bottom"].Value.Trim());
    }

    public static string RemoveImgflipHint(string markdownBody) =>
        ImgflipHintPattern().Replace(markdownBody, string.Empty).Trim();

    public static string? ExtractTldrLine(string markdownBody)
    {
        var lines = markdownBody.Split('\n');
        for (var idx = 0; idx < lines.Length; idx++)
        {
            if (lines[idx].Contains("**TL;DR**"))
            {
                var after = lines[idx].Split("**TL;DR**", 2)[^1].Trim(' ', ':', '\t');
                if (!string.IsNullOrEmpty(after))
                    return after;
                for (var nxt = idx + 1; nxt < lines.Length; nxt++)
                {
                    var stripped = lines[nxt].Trim();
                    if (!string.IsNullOrEmpty(stripped))
                        return stripped;
                }
                break;
            }
        }
        return null;
    }
}
