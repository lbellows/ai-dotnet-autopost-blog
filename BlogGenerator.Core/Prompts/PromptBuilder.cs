using System.Text.RegularExpressions;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.PostGeneration;

namespace BlogGenerator.Core.Prompts;

public static partial class PromptBuilder
{
    // A prompt section drops out entirely when there is nothing to put in it (an archive-free
    // first run, no allowed domains), so the gap it leaves behind gets closed here.
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankRunRegex();

    private static string Tidy(string prompt) =>
        BlankRunRegex().Replace(prompt.ReplaceLineEndings("\n"), "\n\n").Trim();

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

    // Code samples were the weakest part of generated posts: most were 3-4 line fragments the
    // model itself labelled "pseudocode", built on APIs that don't exist. One substantial sample
    // built from verified APIs beats three sketches, and no sample at all beats an invented one.
    internal static string CodeGuidance(GenerationSettings settings) =>
        $"Include AT MOST ONE code block in the whole post, and make it the centerpiece rather than an aside: " +
        $"{settings.CodeSampleMinLines}-{settings.CodeSampleMaxLines} lines that a reader could actually orient themselves with. " +
        "Show a complete unit of work — the relevant using/import lines, real package and type names, and the configuration, " +
        "error path, or call site that makes it make sense — not a disembodied fragment. " +
        "Prefer something non-obvious: a before/after migration, the failure path, or config paired with the code that reads it. " +
        "Only use APIs, package names, and types you can verify from your sources. If you cannot write the sample against a real " +
        "API surface, OMIT the code block entirely and explain the idea in prose — a post with no code is far better than a post " +
        "with invented code, and no reader is expecting a snippet. " +
        "Never label a sample as pseudocode, conceptual, illustrative, or a sketch; never emit NotImplementedException, " +
        "'...' placeholders, or stubbed method bodies. If it needs that disclaimer, it does not belong in the post. " +
        "A block that is only package-install or CLI-invocation lines (e.g. just 'dotnet add package X' or 'azd up') does not " +
        "count as a code sample — fold those lines into a larger example or leave them inline in the prose.";

    private const string HumorGuidance =
        "Keep the tone professional yet witty—sprinkle in light, tasteful humor or asides that help the reader stay engaged.";

    private const string WriterRole =
        "You are a senior technical writer for software engineers working with .NET, Azure, and AI Software.";

    private const string ModeHeader =
        "You MUST always output a complete, publishable blog post, in exactly ONE of two modes:";

    // Curated subset of reliable imgflip top-100 templates, each with its box count
    // and per-box descriptions. The descriptions drive how many pipe-separated texts
    // GPT must supply. This is the single source of truth for the meme catalog; the
    // prompt presents it in a shuffled order each run (see ImgflipGuidance) so the
    // model doesn't keep defaulting to whichever template sits at the top of the list.
    internal static readonly IReadOnlyList<string> ImgflipTemplateCatalog =
    [
        "Drake Hotline Bling(2: reject, prefer)",
        "Distracted Boyfriend(3: label on girlfriend, label on distracted guy, label on other woman)",
        "Two Buttons(3: button 1, button 2, who faces the dilemma — name them, don't describe the sweating)",
        "Expanding Brain(4: small brain, medium brain, large brain, galaxy brain)",
        "Change My Mind(2: bold claim on sign, speaker label)",
        "Gru's Plan(4: step1, step2, step3, step3 goes wrong)",
        "One Does Not Simply(2: 'One does not simply...', the thing you cannot do)",
        "This Is Fine(2: situation label, character label)",
        "Waiting Skeleton(2: what you are waiting for, how long it takes)",
        "Bernie I Am Once Again Asking(2: who is asking, what they are asking for)",
        "They're The Same Picture(3: top banner instruction, left card text, right card text)",
        "Trade Offer(3: I receive, you receive, trader label)",
        "Panik Kalm Panik(3: first panik, kalm, second panik)",
        "Buff Doge vs. Cheems(4: buff doge label, buff doge caption, cheems label, cheems caption)",
        "Left Exit 12 Off Ramp(3: exit you ignore, exit you take, driver label)",
        "Third World Skeptical Kid(2: claim, skeptical reaction)",
    ];

    // Builds the meme guidance with the template catalog presented in a freshly
    // shuffled order so the model is nudged toward variety instead of repeatedly
    // picking the first familiar option (it was over-favoring "Two Buttons").
    internal static string ImgflipGuidance(Random rng)
    {
        var shuffled = ImgflipTemplateCatalog.OrderBy(_ => rng.Next()).ToList();
        return
            "At the most relevant point in the article, output exactly one HTML comment on its own line in this format: " +
            "<!-- meme: template=TEMPLATE_NAME, texts=\"TEXT0|TEXT1|...\" --> " +
            $"Pick TEMPLATE_NAME from this list — the number in parentheses is how many pipe-separated texts to supply: {string.Join(", ", shuffled)}. " +
            "Deliberately vary your choice across posts: pick the template whose format best matches the story's structure " +
            "(e.g., a tradeoff, a progression, a false dilemma, a slow wait) rather than defaulting to the most familiar one. " +
            "Supply exactly as many texts as the box count requires, each under 60 chars, witty, and relevant to the post topic. " +
            "Captions should add meaning the picture doesn't already convey (the situation, the stakes, who is involved) — " +
            "do not narrate what the image visibly shows (e.g. don't label a sweating character 'sweating', or a burning room 'on fire'). " +
            "Do not put the comment inside a code block.";
    }

    // The shared "what a finished post looks like" checklist. Both the single-call
    // providers and the Venice writer stage compose their system prompt around it so
    // the house style stays in exactly one place.
    internal static string GuidanceBlock(GenerationSettings settings, Random rng)
    {
        var guidanceLines = new List<string>
        {
            $"- A single H1 title on the first line. {TitleGuidance}",
            "- Do not include a 'Published', word-count, audience, or tags metadata line in the body; front matter and the site layout already handle that.",
            "- Open with a short summary paragraph immediately after the title (no 'TL;DR' or 'Summary' label or heading — just lead with the prose).",
            "- Clear sections with practical takeaways.",
            $"- {TechGuidance}",
            $"- {HumorGuidance}",
        };
        if (settings.CodeSamplesEnabled)
            guidanceLines.Add($"- {CodeGuidance(settings)}");
        else
            guidanceLines.Add("- Do not include code blocks; explain implementation details in prose instead.");

        if (settings.ImgflipMemeEnabled)
            guidanceLines.Add($"- {ImgflipGuidance(rng)}");
        guidanceLines.Add("- Cautious language for claims; avoid speculation and hallucinations.");
        guidanceLines.Add("- A **Further reading** section listing all source links as plain URLs.");

        return string.Join("\n", guidanceLines);
    }

    // Length, language, and output-format rules — identical for every provider and stage.
    private static string LengthRule(GenerationSettings settings) =>
        $"Length: {settings.PostWordsMin}-{settings.PostWordsMax} words. US English. " +
        (settings.ImgflipMemeEnabled
            ? "Markdown only — the one exception is the meme HTML comment described above, which must be included verbatim."
            : "Markdown only (no HTML).");

    // What the blog has already published, rendered for a prompt. Without this the generator has
    // no memory across runs: the research angles are fixed and the archive is invisible, so a thin
    // freshness window sends every stage back to the same evergreen material. TitleGuidance has
    // always said "never repeat the same title formula across posts", which is not a thing a model
    // can honour without being shown the posts.
    internal static string RecentCoverage(IReadOnlyList<PublishedPost> recentPosts)
    {
        if (recentPosts.Count == 0)
            return string.Empty;

        var lines = recentPosts.Select(post => $"- {post.Date:yyyy-MM-dd} — {post.Title}");
        return $"""
            Already published on this blog, newest first:
            {string.Join("\n", lines)}
            """.ReplaceLineEndings("\n").Trim();
    }

    // The writer's rule about that list. Stated as "pick different ground", not "never mention
    // these words": a genuine in-window story about Azure pricing is still the right post to write,
    // and a model told to avoid a subject outright will write around the news it actually found.
    private static string RecentCoverageRule(IReadOnlyList<PublishedPost> recentPosts)
    {
        if (recentPosts.Count == 0)
            return string.Empty;

        return RecentCoverage(recentPosts) + "\n\n" +
               "Do not write another post on the same subject as any of those, and do not reuse their title " +
               "structures. If the only strong material you have overlaps one of them, lead with what is genuinely " +
               "new since it rather than restating it, and say plainly what changed. A narrower, less obvious topic " +
               "the list does not cover is better than a fourth pass over one it does. Never refer to these posts, " +
               "to this list, or to the blog's own archive in the article itself.";
    }

    // The post ships verbatim, so the model must never break character to talk to us.
    private static string NeverBreakCharacterRule(bool fromDossier)
    {
        var mentions = fromDossier ? "these instructions, the dossier, or" : "these instructions or";
        return "Never refuse, never ask the reader a question, never explain that sources were missing, and never " +
               $"address the user or mention {mentions} which mode you chose. The output is published verbatim, so " +
               "it must read as a finished, self-assured post either way.";
    }

    public static PromptContext Build(
        GenerationSettings settings,
        DateOnly? today = null,
        Random? rng = null,
        IReadOnlyList<PublishedPost>? recentPosts = null)
    {
        rng ??= Random.Shared;
        recentPosts ??= [];
        var currentDay = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var recentStart = currentDay.AddDays(-settings.RecentWindowDays);
        var userInstructionText = string.Join("\n",
            UserInstructionItems(settings, currentDay, recentStart)
                .Select((item, idx) => $"{idx + 1}) {item}"));
        var primaryLine = !string.IsNullOrEmpty(settings.TopicUrl)
            ? $"Primary requested link: {settings.TopicUrl}\n"
            : "";

        var guidanceBlock = GuidanceBlock(settings, rng);
        var coverageRule = RecentCoverageRule(recentPosts);

        var systemPrompt = $"""
            {WriterRole}
            Use the web_search tool to gather several fresh, reputable sources about current AI developments
            that impact developers. Then write a grounded Markdown blog post with:

            {guidanceBlock}

            {LengthRule(settings)}
            {ModeHeader}
            (A) NEWS MODE — only if you find a genuinely fresh lead story whose primary announcement falls inside the
            window. Lead with it and you may frame it as recent/this-week.
            (B) EVERGREEN MODE — if nothing inside the window qualifies. Write a timeless, pragmatic piece for the same
            audience on a still-relevant .NET/Azure/AI engineering topic. Do NOT reach for an older item (a release
            from weeks or months ago) and dress it up as fresh, and do NOT use time-sensitive framing like "this week",
            "the freshest development", or "just landed". Write it as evergreen guidance, not as news.

            {coverageRule}

            {NeverBreakCharacterRule(fromDossier: false)}
            If the web_search tool is unavailable, do not emit tool-call markup (e.g., <|start|> tokens); respond directly with the final article.
            """;

        var userPrompt = $"""
            Topic focus / audience: {settings.TopicHint}
            {primaryLine}
            Instructions:
            {userInstructionText}
            """.ReplaceLineEndings("\n").Trim();

        return new PromptContext(
            Today: currentDay,
            RecentStartDate: recentStart,
            SystemPrompt: Tidy(systemPrompt),
            UserPrompt: userPrompt,
            GuidanceBlock: guidanceBlock,
            RecentPosts: recentPosts,
            RecentCoverageRule: coverageRule);
    }

    public static string ModeInstructions(DateOnly today, int recentWindowDays)
    {
        if (today.DayOfWeek == DayOfWeek.Sunday)
            return "Sunday is synopsis day: weave the freshest breaking stories into a cohesive weekly roundup " +
                   "that also previews what's next (e.g., 2025 readiness tips, roadmap considerations).";

        return $"NEWS MODE: pick one laser-focused story whose primary announcement happened within the last {recentWindowDays} day(s) " +
               "and dive deep into its implications. The hook must be genuinely from that window — do NOT present a release from " +
               $"weeks or months ago (e.g. a stable 1.0, or anything older than {recentWindowDays} days) as if it just happened. " +
               "If no story qualifies, switch to EVERGREEN MODE: a timeless deep-dive with no time-sensitive framing. " +
               "Either way, go deep on one topic — avoid broad grab-bag summaries.";
    }

    public static List<string> UserInstructionItems(
        GenerationSettings settings, DateOnly today, DateOnly recentStartDate)
    {
        var items = new List<string>
        {
            $"Use the web_search tool to find reputable sources. For NEWS MODE, the lead story's primary announcement " +
            $"must be dated between {recentStartDate:yyyy-MM-dd} and {today:yyyy-MM-dd}. Before concluding that no fresh " +
            $"story exists, make AT LEAST 4 distinct search attempts with genuinely different queries and angles — e.g. " +
            $"vendor engineering blogs (Microsoft/Azure/OpenAI/Anthropic/GitHub), official changelogs and release notes, " +
            $"GitHub releases, and developer news aggregators — not 4 rewordings of the same query. Only after that honest " +
            $"effort, if nothing in the date range qualifies, do NOT reframe an older announcement as new — write an " +
            $"EVERGREEN piece with no time-sensitive framing instead. Supporting/context sources may be older in either mode.",
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

    // Venice's web search is a single-shot retrieval pass injected into the prompt, not an
    // agentic tool loop the model can call repeatedly. To honor the "at least 4 distinct
    // search attempts" requirement we instead issue one research call per angle below and
    // merge the results, which gives the writer stage genuinely different source material.
    public static List<string> ResearchAngles(GenerationSettings settings, DateOnly today, DateOnly recentStartDate)
    {
        var window = $"between {recentStartDate:yyyy-MM-dd} and {today:yyyy-MM-dd}";
        var preferred = settings.AllowedDomains.Count > 0
            ? $" Prefer coverage from: {string.Join(", ", settings.AllowedDomains)}."
            : string.Empty;

        var angles = new List<string>
        {
            $"Vendor engineering blogs: what did Microsoft, Azure, GitHub, OpenAI, or Anthropic announce {window} " +
            $"that changes how developers build software? Topic focus: {settings.TopicHint}{preferred}",

            $"Official release notes and changelogs: which .NET, ASP.NET Core, Azure SDK, Azure AI Foundry, or " +
            $"GitHub Copilot releases shipped {window}? Include exact version numbers and release dates.{preferred}",

            $"GitHub releases and developer tooling: which AI or .NET developer SDKs, CLIs, or libraries cut a new " +
            $"release {window}? Name the repository, the version, and what changed for consumers.",

            $"Developer news coverage and analysis: what are technology publications reporting {window} about AI " +
            $"tooling that matters to engineers shipping on .NET and Azure? Include cost, latency, and API details.",
        };

        if (!string.IsNullOrEmpty(settings.TopicUrl))
        {
            angles.Insert(0,
                $"Summarize this specific announcement and gather corroborating coverage of it: {settings.TopicUrl}");
        }

        return angles;
    }

    // The dossier's shape is a contract between whichever stage researches and the writer stage,
    // which parses it by section — so every research prompt describes it from this one place.
    private const string DossierShape = """
        ## In-window findings
        Items whose PRIMARY announcement date falls inside the freshness window. For each: what shipped, who
        shipped it, the exact date, and why an engineer should care. If there are none, write "None." and say so
        plainly. Never stretch an older item into the window.

        ## Context
        Relevant older or undated background that would strengthen a post. Mark each with its real date.

        ## Sources
        Every URL you actually used, one per line as a plain URL followed by a short title.

        Start every finding and context item with the platform it belongs to, as the source states it: [.NET],
        [Python], [JavaScript], [Java], [Go], or [Azure service] for a hosted service any language can call. Repos
        like microsoft/agent-framework ship several languages from one release list, so tag each item on its own
        rather than the list as a whole. If the source does not say which platform an item is for, tag it
        [Unclear] — never assume .NET.
        """;

    // The research stages get the archive too, not just the writer. Telling only the writer to avoid
    // a subject while handing it a dossier made entirely of that subject produces a worse post, not a
    // different one — the avoidance has to reach the stage that chooses what to go and read.
    private static string ResearchCoverageRule(IReadOnlyList<PublishedPost> recentPosts)
    {
        if (recentPosts.Count == 0)
            return string.Empty;

        return RecentCoverage(recentPosts) + "\n\n" +
               "The brief is for the next post, so it must open ground those have not covered. Findings that would " +
               "support a rewrite of one of them are the least useful thing you can bring back: if something you " +
               "find overlaps one, report only what is new since that date and say what changed. Spend your later " +
               "passes looking somewhere the list does not already go.";
    }

    public static string ResearchSystemPrompt(
        GenerationSettings settings,
        DateOnly today,
        DateOnly recentStartDate,
        IReadOnlyList<PublishedPost>? recentPosts = null)
    {
        return Tidy($"""
            You are a research assistant for a technical blog written for software engineers shipping on .NET, Azure,
            and AI platforms. Today is {today:yyyy-MM-dd}. The freshness window for news is
            {recentStartDate:yyyy-MM-dd} to {today:yyyy-MM-dd}.

            You are given web search results. Produce a factual research brief in Markdown — notes only, never a
            finished article — with these sections:

            {DossierShape}

            {ResearchCoverageRule(recentPosts ?? [])}

            Rules: copy dates and version numbers exactly as the sources state them; never invent a URL, a date, or a
            version. If the search results are thin, say the results were thin rather than filling the gap with
            recollection. Do not use footnote or citation markers.
            """);
    }

    /// <summary>
    /// The research prompt for a model that reads publisher feeds itself, one tool call at a time.
    /// Unlike the Venice brain stage it is not handed results — it has to go and get them, so the
    /// prompt is mostly method: what to call, in what order, and when to stop.
    /// </summary>
    public static string FeedResearchSystemPrompt(
        GenerationSettings settings,
        DateOnly today,
        DateOnly recentStartDate,
        IReadOnlyList<PublishedPost>? recentPosts = null)
    {
        return Tidy($"""
            You are a research assistant for a technical blog written for software engineers shipping on .NET, Azure,
            and AI platforms. Today is {today:yyyy-MM-dd}. The freshness window for news is
            {recentStartDate:yyyy-MM-dd} to {today:yyyy-MM-dd}.

            You have two tools, and they are your ONLY source of facts:
            - list_recent_posts — what the publishers we trust have posted, already sorted into in-window and older.
            - fetch_page — the full text of one of those articles.

            You have no other knowledge of what has happened recently. Anything you did not read through a tool in
            this conversation does not go in the brief, however sure you are of it.

            Method:
            1. Call list_recent_posts with no arguments to see the whole window across every source.
            2. Make several more passes with different query keywords or a single source, to surface things the first
               listing buried. Genuinely different angles — releases, SDKs, models, tooling, outages — not rewordings
               of one query.
            3. Call fetch_page on every article you intend to report. A feed summary tells you a thing exists; it does
               not tell you what changed, and the post needs the second one.
            4. Then, and only then, write the brief.

            Produce a factual research brief in Markdown — notes only, never a finished article — with these sections:

            {DossierShape}

            {ResearchCoverageRule(recentPosts ?? [])}

            Rules: copy dates and version numbers exactly as the sources state them; never invent a URL, a date, or a
            version. Do not use footnote or citation markers. Only list a URL under Sources if a tool returned that
            exact URL in this conversation. If the
            window turned up nothing worth leading with, say so plainly under In-window findings and put the best
            background you found under Context — an honest "None." is far more useful than a stretched item.
            """);
    }

    public static string FeedResearchUserPrompt(GenerationSettings settings, DateOnly today, DateOnly recentStartDate)
    {
        var focus = string.Join("\n", ResearchAngles(settings, today, recentStartDate)
            .Select((angle, index) => $"{index + 1}) {angle}"));

        return $"""
            Topic focus / audience: {settings.TopicHint}

            Research the angles below, then write the brief. They are what to look for, not queries to type
            verbatim — the tools match keywords against article titles and summaries, so search with the words a
            headline would actually use.

            {focus}

            Start now with list_recent_posts.
            """.ReplaceLineEndings("\n").Trim();
    }

    // The writer stage has no search of its own — it composes from the research dossier, so
    // the guidance is identical to the single-call prompt minus the web_search instructions.
    public static string WriterSystemPrompt(PromptContext ctx, GenerationSettings settings)
    {
        return Tidy($"""
            {WriterRole}
            A research dossier gathered from live web search is supplied in the user message. Write a grounded
            Markdown blog post from it with:

            {ctx.GuidanceBlock}

            {LengthRule(settings)}
            {ModeHeader}
            (A) NEWS MODE — only if the dossier reports a genuinely fresh lead story whose primary announcement falls
            inside the window {ctx.RecentStartDate:yyyy-MM-dd} to {ctx.Today:yyyy-MM-dd}. Lead with it and you may
            frame it as recent/this-week.
            (B) EVERGREEN MODE — if the dossier's "In-window findings" section is empty or nothing in it qualifies.
            Write a timeless, pragmatic piece for the same audience on a still-relevant .NET/Azure/AI engineering
            topic. Do NOT reach for an older item from the dossier and dress it up as fresh, and do NOT use
            time-sensitive framing like "this week", "the freshest development", or "just landed".

            {ctx.RecentCoverageRule}

            The readers ship on .NET. Leave out dossier items tagged [Python], [JavaScript], [Java], or [Go], and
            never present one of them as a .NET change. Use an [Unclear] item only as background, not as a .NET
            claim.

            {NeverBreakCharacterRule(fromDossier: true)}
            You have no search tool in this step: every URL you print must appear verbatim in the dossier. Never
            invent a link, a date, or a version number, and never emit footnote markers or tool-call markup.
            """);
    }

    public static string WriterUserPrompt(PromptContext ctx, string researchDossier)
    {
        return $"""
            {ctx.UserPrompt}

            The research below was gathered by live web search. Treat it as your only source of facts and URLs.

            ---
            {researchDossier}
            ---
            """.ReplaceLineEndings("\n").Trim();
    }
}
