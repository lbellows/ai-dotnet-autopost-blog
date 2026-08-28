using System.Text.RegularExpressions;

namespace BlogGenerator.Core.Memes;

// Texts maps to each text box in order; length should match the template's box_count.
public record ImgflipHint(string TemplateName, IReadOnlyList<string> Texts);

public static partial class MemeExtractor
{
    // Matches: <!-- meme: template=Gru's Plan, texts="A|B|C|D" -->
    [GeneratedRegex(
        @"<!--\s*meme:\s*template=(?<template>[^,]+),\s*texts=""(?<texts>[^""]*)""\s*-->",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImgflipHintPattern();

    public static ImgflipHint? ExtractImgflipHint(string markdownBody)
    {
        var match = ImgflipHintPattern().Match(markdownBody);
        if (!match.Success) return null;
        var texts = match.Groups["texts"].Value
            .Split('|')
            .Select(t => t.Trim())
            .ToList();
        return new ImgflipHint(match.Groups["template"].Value.Trim(), texts);
    }

    public static string RemoveImgflipHint(string markdownBody) =>
        ImgflipHintPattern().Replace(markdownBody, string.Empty).Trim();

    // Replaces the hint comment in-place with the rendered image markdown,
    // preserving GPT's chosen position in the article.
    public static string ReplaceImgflipHint(string markdownBody, string imageUrl, string title)
    {
        var altText = $"{title} meme";
        if (altText.Length > 80) altText = altText[..77] + "...";
        var imageMarkdown = $"![{altText}]({imageUrl})";
        return ImgflipHintPattern().Replace(markdownBody, imageMarkdown);
    }
}
