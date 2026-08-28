using System.Text.RegularExpressions;

namespace BlogGenerator.Core.PostGeneration;

public static partial class TagInferrer
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "into", "from", "your", "you",
        "are", "was", "will", "have", "using", "about", "what", "need", "know",
        "over", "its", "their", "those", "these",
        "such", "tips", "guide", "latest", "today", "tomorrow", "overview", "intro",
        "developers", "developer", "engineers", "engineer", "update", "updates",
        "insights", "insight", "future", "news", "deep", "dive", "focus", "weekly",
        "daily", "report", "analysis", "roundup", "learn", "learning", "build",
        "building", "powered", "power", "next", "gen", "generative", "recent",
        "versus", "plus", "look", "back", "ahead", "quick", "start", "setup",
        "create", "creating", "created", "some", "page", "pages", "step", "steps",
        // Generic English that scores well on heading frequency but makes a useless tag.
        // Without these the inferrer emits filler like "between", "bring", or "can".
        "all", "also", "another", "any", "around", "because", "been", "before", "being",
        "below", "between", "beyond", "both", "bring", "brings", "but", "can", "cant",
        "could", "did", "does", "doing", "done", "down", "during", "each", "even", "ever",
        "every", "few", "get", "gets", "getting", "goes", "going", "gone", "got", "had",
        "has", "her", "here", "hers", "him", "his", "how", "into", "isnt", "just", "least",
        "less", "let", "lets", "like", "made", "make", "makes", "making", "many", "may",
        "might", "more", "most", "much", "must", "near", "never", "not", "now", "off",
        "often", "once", "one", "only", "onto", "other", "others", "ought", "our", "ours",
        "out", "own", "put", "puts", "putting", "same", "says", "she", "should", "since",
        "still", "take", "takes", "taking", "than", "then", "there", "they", "thing",
        "things", "though", "through", "took", "toward", "towards", "under", "until",
        "upon", "very", "want", "wants", "were", "when", "where", "whether", "which",
        "while", "who", "whom", "whose", "why", "without", "wont", "would", "yet",
    };

    [GeneratedRegex(@"[A-Za-z0-9\+\.\-]+")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"[a-z]")]
    private static partial Regex HasLowerRegex();

    [GeneratedRegex(@"[^\w\+\-\.]")]
    private static partial Regex NonTagCharRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDashRegex();

    [GeneratedRegex(@"(?:https?://|www\.)\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    public static List<string> Infer(string markdownBody, string? model)
    {
        var candidates = new Dictionary<string, int>(StringComparer.Ordinal);
        var sections = new List<string>();

        foreach (var line in markdownBody.Split('\n'))
        {
            var stripped = line.Trim();
            if (stripped.StartsWith('#'))
                sections.Add(stripped.TrimStart('#').Trim());
            else if (stripped.StartsWith("**TL;DR**", StringComparison.OrdinalIgnoreCase))
                sections.Add(stripped.Split("**TL;DR**", 2)[^1].Trim(' ', ':'));
        }

        var textBlob = sections.Count > 0 ? string.Join(" ", sections) : markdownBody;

        foreach (Match m in TokenRegex().Matches(textBlob))
        {
            var normalized = NormalizeTag(m.Value);
            if (!string.IsNullOrEmpty(normalized))
                candidates[normalized] = candidates.GetValueOrDefault(normalized) + 1;
        }

        // Headings are short, so most candidates tie at a single occurrence and the tiebreak
        // decides the tag list. Alphabetical order made that "actually" and "already"; whole-body
        // frequency makes it whatever the post is actually about.
        var bodyFrequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match m in TokenRegex().Matches(markdownBody))
        {
            var normalized = NormalizeTag(m.Value);
            if (!string.IsNullOrEmpty(normalized))
                bodyFrequency[normalized] = bodyFrequency.GetValueOrDefault(normalized) + 1;
        }

        // Candidates come from headings, which are Title Case, so capitalization there cannot
        // tell "Azure" from "Actually". Body prose can: real tags are proper nouns or versioned
        // identifiers, and they show up capitalized mid-sentence. Rank those first so generic
        // English only fills leftover slots instead of outranking product names.
        var salient = CollectSalientTokens(markdownBody);

        // A post whose headings are conversational ("What an AI gateway actually does") offers
        // almost no usable candidates, so the list fills with filler. Let salient body terms
        // compete too; heading hits still outweigh them via the x3 weight below.
        foreach (var token in salient)
            candidates.TryAdd(token, 0);

        var tags = new List<string>();
        foreach (var (token, _) in candidates
                     .OrderByDescending(kv => salient.Contains(kv.Key))
                     .ThenByDescending(kv => (kv.Value * 3) + bodyFrequency.GetValueOrDefault(kv.Key))
                     .ThenBy(kv => kv.Key))
        {
            if (!tags.Contains(token))
                tags.Add(token);
            if (tags.Count >= 5)
                break;
        }

        var lowerMd = markdownBody.ToLowerInvariant();
        if (lowerMd.Contains("ai") && !tags.Contains("ai"))
            tags.Add("ai");

        var modelTag = (model ?? "claude").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(modelTag) && !tags.Contains(modelTag))
            tags.Add(modelTag);

        if (tags.Count == 0)
            tags = ["ai", string.IsNullOrEmpty(modelTag) ? "claude" : modelTag];

        if (tags.Count > 6)
        {
            var core = tags.Where(t => t != modelTag).Take(5).ToList();
            if (!string.IsNullOrEmpty(modelTag))
                core.Add(modelTag);
            tags = core;
        }

        return tags;
    }

    /// <summary>
    /// Tokens from body prose that look like names rather than vocabulary: capitalized somewhere
    /// other than the start of a sentence (Azure, Copilot, MCP), or carrying a digit, dot, or
    /// hyphen (.net, gpt-5, 2026). Headings are skipped because Title Case makes every word look
    /// like a proper noun.
    /// </summary>
    // "github.blog" and "learn.microsoft.com" are sources, not subjects. A leading dot means
    // the token is a platform name (".net"), not a host, so those are left alone.
    internal static bool LooksLikeDomain(string token)
    {
        if (token.StartsWith('.') || !token.Contains('.'))
            return false;

        var lastLabel = token[(token.LastIndexOf('.') + 1)..];
        return DomainSuffixes.Contains(lastLabel);
    }

    internal static HashSet<string> CollectSalientTokens(string markdownBody)
    {
        var salient = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in markdownBody.Split('\n'))
        {
            var stripped = line.Trim();
            if (stripped.Length == 0 || stripped.StartsWith('#'))
                continue;

            // Links are dense with dot- and hyphen-bearing tokens that look like identifiers
            // (github.blog, en-us, a whole post slug), so drop them before judging salience.
            stripped = UrlRegex().Replace(stripped, " ");

            foreach (Match m in TokenRegex().Matches(stripped))
            {
                var looksLikeIdentifier = m.Value.Any(c => char.IsDigit(c) || c is '.' or '-' or '+');
                var capitalizedMidSentence =
                    char.IsUpper(m.Value[0]) && !IsSentenceStart(stripped, m.Index);

                if (!looksLikeIdentifier && !capitalizedMidSentence)
                    continue;

                var normalized = NormalizeTag(m.Value);
                if (!string.IsNullOrEmpty(normalized))
                    salient.Add(normalized);
            }
        }

        return salient;
    }

    // Walks back past whitespace and markdown decoration to decide whether a token opens a
    // sentence, list item, or table cell — positions where capitalization means nothing.
    private static bool IsSentenceStart(string line, int index)
    {
        var i = index - 1;
        while (i >= 0 && (char.IsWhiteSpace(line[i]) || line[i] is '*' or '_' or '`' or '(' or '[' or '"' or '\''))
            i--;

        return i < 0 || line[i] is '.' or '!' or '?' or ':' or ';' or '|' or '>' or '-';
    }

    // Domain suffixes that mark a token as a source link rather than a topic. Deliberately
    // excludes "net" so ".net" and "asp.net" survive as tags.
    private static readonly HashSet<string> DomainSuffixes = new(StringComparer.Ordinal)
    {
        "com", "org", "io", "blog", "dev", "ai", "co", "ms", "uk", "gov", "edu", "news", "app",
    };

    internal static string NormalizeTag(string token)
    {
        token = token.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(token) || Stopwords.Contains(token) || token.Length < 3)
            return "";
        if (LooksLikeDomain(token))
            return "";
        if (!HasLowerRegex().IsMatch(token))
            return "";
        token = NonTagCharRegex().Replace(token, "-");
        token = MultiDashRegex().Replace(token, "-").Trim('-');
        return token;
    }
}
