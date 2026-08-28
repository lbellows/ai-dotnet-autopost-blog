using System.Text.RegularExpressions;
using BlogGenerator.Core.Configuration;

namespace BlogGenerator.Core.PostGeneration;

/// <summary>
/// Reports code blocks that fall short of the house rules in
/// <see cref="Prompts.PromptBuilder.CodeGuidance"/>. This is deliberately advisory: the pipeline
/// publishes unattended, so silently deleting a block would risk orphaning the prose that
/// introduces it. Warnings land in the workflow log instead, where they show whether the prompt
/// is holding up over time.
/// </summary>
public static partial class CodeSampleLinter
{
    // Opening or closing fence of a Markdown code block, capturing the info string (language).
    [GeneratedRegex(@"^\s*```(.*)$")]
    private static partial Regex FenceRegex();

    // The model used to hedge its samples with these words; the guidance now forbids them.
    [GeneratedRegex(@"(?i)\bpseudo-?code\b|\bconceptual\b|\billustrative\b|\bsketch\b|not (a )?production-ready")]
    private static partial Regex DisclaimerRegex();

    // Stubbed-out bodies and elided arguments that make a sample impossible to run.
    [GeneratedRegex(@"NotImplementedException|\(\s*\.\.\.\s*\)|^\s*//\s*\.\.\.\s*$", RegexOptions.Multiline)]
    private static partial Regex StubRegex();

    // A line that only installs a package or invokes a CLI verb, with no surrounding example.
    [GeneratedRegex(@"^\s*(dotnet add package|dotnet new|npm i(nstall)?|azd (init|up|auth|provision|deploy)|az login)\b")]
    private static partial Regex InstallOnlyRegex();

    public static IReadOnlyList<string> Inspect(string markdownBody, GenerationSettings settings)
    {
        var warnings = new List<string>();
        var blocks = ExtractBlocks(markdownBody);

        if (settings.CodeSamplesEnabled && blocks.Count > 1)
            warnings.Add($"{blocks.Count} code blocks; the guidance asks for at most one.");

        if (!settings.CodeSamplesEnabled && blocks.Count > 0)
            warnings.Add($"{blocks.Count} code block(s) present but CodeSamplesEnabled is false.");

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var label = $"block {i + 1}{(string.IsNullOrEmpty(block.Language) ? "" : $" ({block.Language})")}";

            if (DisclaimerRegex().IsMatch(block.Body))
                warnings.Add($"{label}: labelled as pseudocode/conceptual/illustrative.");

            if (StubRegex().IsMatch(block.Body))
                warnings.Add($"{label}: contains a stub or '...' placeholder.");

            if (IsInstallOnly(block.Body))
                warnings.Add($"{label}: only package-install or CLI-invocation lines.");
            else if (settings.CodeSamplesEnabled && block.LineCount < settings.CodeSampleMinLines)
                warnings.Add(
                    $"{label}: {block.LineCount} lines, below the {settings.CodeSampleMinLines}-line minimum.");
            else if (settings.CodeSamplesEnabled && block.LineCount > settings.CodeSampleMaxLines)
                warnings.Add(
                    $"{label}: {block.LineCount} lines, above the {settings.CodeSampleMaxLines}-line maximum.");
        }

        return warnings;
    }

    private static bool IsInstallOnly(string body)
    {
        var meaningful = body
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith("//"))
            .ToList();

        return meaningful.Count > 0 && meaningful.All(line => InstallOnlyRegex().IsMatch(line));
    }

    private static List<CodeBlock> ExtractBlocks(string markdownBody)
    {
        var blocks = new List<CodeBlock>();
        var open = false;
        var language = string.Empty;
        var body = new List<string>();

        foreach (var line in markdownBody.ReplaceLineEndings("\n").Split('\n'))
        {
            var fence = FenceRegex().Match(line);
            if (!fence.Success)
            {
                if (open) body.Add(line);
                continue;
            }

            if (!open)
            {
                open = true;
                language = fence.Groups[1].Value.Trim();
                body.Clear();
            }
            else
            {
                open = false;
                blocks.Add(new CodeBlock(language, string.Join("\n", body), body.Count));
            }
        }

        // An unterminated fence still describes a block worth inspecting.
        if (open)
            blocks.Add(new CodeBlock(language, string.Join("\n", body), body.Count));

        return blocks;
    }

    private sealed record CodeBlock(string Language, string Body, int LineCount);
}
