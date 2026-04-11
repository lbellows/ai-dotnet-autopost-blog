using System.Text;
using BlogGenerator.Core.Configuration;

namespace BlogGenerator.Core.Prompts;

public static class PromptBuilder
{
    private const string TechGuidance =
        "Highlight at least one of these ecosystems where relevant: .NET, Azure, or any AI related software. " +
        "Choose whichever best fits the story; covering all three is optional.";

    private const string TitleGuidance =
        "Title must be specific and varied in structure. " +
        "Do NOT use the formula \"[Product]: What .NET/Azure engineers should [do/know/care about]\". " +
        "Instead pick a structure that fits the story, for example: " +
        "a direct declaration (\"GPT-5 Is Now GA\"), " +
        "a numbered insight (\"5 Breaking Changes in .NET 10 You Can't Ignore\"), " +
        "a question (\"Is Azure AI Foundry Ready for Production?\"), " +
        "a contrast (\"Old Pattern vs. New: Migrating Azure SDK Auth in 2026\"), " +
        "or a plain news headline (\"GitHub Copilot SDK Enters Public Preview\"). " +
        "Rotate the structure—never repeat the same title formula across posts.";

    private const string HumorGuidance =
        "Keep the tone professional yet witty—sprinkle in light, tasteful humor or asides that help the reader stay engaged.";

    // Curated subset of reliable imgflip top-100 template names.
    private const string ImgflipTemplateList =
        "Drake Hotline Bling, Distracted Boyfriend, Two Buttons, " +
        "Expanding Brain, Change My Mind, Gru's Plan, " +
        "One Does Not Simply, This Is Fine, " +
        "Waiting Skeleton, Bernie I Am Once Again Asking, " +
        "They're The Same Picture, Trade Offer, " +
        "Panik Kalm Panik, Buff Doge vs. Cheems, " +
        "Left Exit 12 Off Ramp, Third World Skeptical Kid";

    private const string ImgflipGuidance =
        "At the most relevant point in the article, output exactly one HTML comment on its own line in this format: " +
        "<!-- meme: template=TEMPLATE_NAME, top=\"TOP TEXT\", bottom=\"BOTTOM TEXT\" --> " +
        $"Pick TEMPLATE_NAME from this list (use the name exactly as written): {ImgflipTemplateList}. " +
        "Choose whichever template best fits the tone of the story. " +
        "Keep TOP TEXT and BOTTOM TEXT short (under 60 chars each), witty, and relevant to the post topic. " +
        "Do not put the comment inside a code block.";

    public static PromptContext Build(GenerationSettings settings, DateOnly? today = null)
    {
        var currentDay = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var recentStart = currentDay.AddDays(-settings.RecentWindowDays);
        var modeText = ModeInstructions(currentDay, settings.RecentWindowDays);
        var userItems = UserInstructionItems(settings, currentDay, recentStart);
        var userInstructionText = string.Join("\n",
            userItems.Select((item, idx) => $"{idx + 1}) {item}"));
        var primaryLine = !string.IsNullOrEmpty(settings.TopicUrl)
            ? $"Primary requested link: {settings.TopicUrl}\n"
            : "";

        var guidanceLines = new List<string>
        {
            $"- A single H1 title on the first line. {TitleGuidance}",
            "- Do not include a 'Published', word-count, audience, or tags metadata line in the body; front matter and the site layout already handle that.",
            "- A short **TL;DR** section.",
            "- Clear sections with practical takeaways (code or CLI snippets welcome).",
            $"- {TechGuidance}",
            $"- {HumorGuidance}",
        };
        if (settings.ImgflipMemeEnabled)
            guidanceLines.Add($"- {ImgflipGuidance}");
        guidanceLines.Add("- Cautious language for claims; avoid speculation and hallucinations.");
        guidanceLines.Add("- A **Further reading** section listing all source links as plain URLs.");

        var guidanceBlock = string.Join("\n", guidanceLines);

        var systemPrompt = $"""
            You are a senior technical writer for software engineers working with .NET, Azure, and AI Software.
            Use the web_search tool to gather several fresh, reputable sources about current AI developments
            that impact developers. Then write a grounded Markdown blog post with:

            {guidanceBlock}

            Length: {settings.PostWordsMin}-{settings.PostWordsMax} words. US English. Markdown only (no HTML).
            If web search fails or yields little, write a pragmatic evergreen piece for the same audience.
            If the web_search tool is unavailable, do not emit tool-call markup (e.g., <|start|> tokens); respond directly with the final article.
            """.ReplaceLineEndings("\n").Trim();

        var userPrompt = $"""
            Topic focus / audience: {settings.TopicHint}
            {primaryLine}
            Instructions:
            {userInstructionText}
            """.ReplaceLineEndings("\n").Trim();

        return new PromptContext(
            Today: currentDay,
            RecentStartDate: recentStart,
            ModeInstructions: modeText,
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            UserInstructionItems: userItems,
            PrimaryLinkLine: primaryLine);
    }

    public static string ModeInstructions(DateOnly today, int recentWindowDays)
    {
        if (today.DayOfWeek == DayOfWeek.Sunday)
            return "Sunday is synopsis day: weave the freshest breaking stories into a cohesive weekly roundup " +
                   "that also previews what's next (e.g., 2025 readiness tips, roadmap considerations).";

        return $"Pick one laser-focused story or product update from the last {recentWindowDays} day(s) " +
               "and dive deep into its implications. Avoid broad grab-bag summaries.";
    }

    public static List<string> UserInstructionItems(
        GenerationSettings settings, DateOnly today, DateOnly recentStartDate)
    {
        var items = new List<string>
        {
            $"Use the web_search tool to find 4-6 fresh, reputable sources published between " +
            $"{recentStartDate:yyyy-MM-dd} and {today:yyyy-MM-dd} (or as close as possible).",
            ModeInstructions(today, settings.RecentWindowDays),
            "Synthesize the key points that matter to engineers (cost, latency, APIs, integration steps).",
            "Cite sources inline where appropriate and list all links at the end in a 'Further reading' list.",
        };

        if (!string.IsNullOrEmpty(settings.TopicUrl))
        {
            items.Insert(2,
                "Treat the primary requested link as the anchor narrative—summarize it first, then expand with corroborating context.");
        }

        if (settings.AllowedDomains.Count > 0)
        {
            items.Add(
                $"Prefer sources from these domains when they have relevant coverage: {string.Join(", ", settings.AllowedDomains)}.");
        }

        if (settings.BlockedDomains.Count > 0)
        {
            items.Add(
                $"Avoid sources from these domains unless there is no credible alternative: {string.Join(", ", settings.BlockedDomains)}.");
        }

        return items;
    }

    public static List<Dictionary<string, object>> BuildChatMessages(PromptContext ctx)
    {
        return
        [
            new()
            {
                ["role"] = "system",
                ["content"] = new List<object>
                {
                    new Dictionary<string, string> { ["type"] = "text", ["text"] = ctx.SystemPrompt }
                }
            },
            new()
            {
                ["role"] = "user",
                ["content"] = new List<object>
                {
                    new Dictionary<string, string> { ["type"] = "text", ["text"] = ctx.UserPrompt }
                }
            }
        ];
    }

    public static List<Dictionary<string, object>> EmptyResponseRetryInstruction()
    {
        return
        [
            new()
            {
                ["role"] = "user",
                ["content"] = new List<object>
                {
                    new Dictionary<string, string>
                    {
                        ["type"] = "text",
                        ["text"] = "The previous response was empty. Provide the complete Markdown article now with an H1 title, " +
                                   "a **TL;DR** section, practical sections, and a **Further reading** list. Do not mention tool usage."
                    }
                }
            }
        ];
    }

    public static List<Dictionary<string, object>> MarkupRetryInstruction(bool toolMarkupPresent)
    {
        var intro = toolMarkupPresent
            ? "The previous response included raw tool-call markup."
            : "The previous response did not deliver the final Markdown article.";

        var text = $"{intro} Web search is unavailable in this environment. Reply now with a complete Markdown post " +
                   "that includes an H1 title, a **TL;DR** section, practical sections, and a **Further reading** list. " +
                   "Do not emit tool-call markup, <|...|> tokens, or describe the attempt; output only the article.";

        return
        [
            new()
            {
                ["role"] = "user",
                ["content"] = new List<object>
                {
                    new Dictionary<string, string> { ["type"] = "text", ["text"] = text }
                }
            }
        ];
    }
}
