using System.Text;
using System.Text.Json;
using BlogGenerator.Core.Configuration;

namespace BlogGenerator.Core.Research;

/// <summary>
/// The research surface for a model that has no search provider behind it: a fixed list of
/// publisher feeds and the ability to read one of the articles they link to.
///
/// There is no keyword search API here and no crawling. That is the design, not a limitation we
/// worked around — feeds are already dated, already scoped to publishers we trust, and cost
/// nothing to read, which is exactly what a "what shipped in the last N days" question needs. The
/// model filters by keyword over what the feeds returned, so <c>query</c> narrows a known list
/// rather than reaching out to the open web.
///
/// The in-window/older split is computed here rather than left to the model on purpose: local
/// models are unreliable at date arithmetic, and a stage whose entire job is a freshness window
/// should not depend on one subtracting dates correctly.
/// </summary>
public sealed class FeedResearchTools(HttpClient httpClient, GenerationSettings settings) : IResearchToolset
{
    private readonly Dictionary<string, IReadOnlyList<FeedItem>> cache = new(StringComparer.OrdinalIgnoreCase);

    // Every article URL a feed has handed back this run. This is what makes "the url must be one
    // you saw in a listing" enforceable rather than merely requested: a publisher whose feed lives
    // on one host and whose articles live on another (feed.infoq.com vs www.infoq.com) is handled
    // exactly, with no guessing at which parent domain a host belongs to.
    private readonly HashSet<string> seenUrls = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
    private DateOnly recentStart = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Binds the toolset to one run's freshness window. A run generates a single post, so the
    /// window is per-process state rather than a parameter threaded through every call.
    /// </summary>
    public FeedResearchTools ForWindow(DateOnly runToday, DateOnly runRecentStart)
    {
        today = runToday;
        recentStart = runRecentStart;
        return this;
    }

    public IReadOnlyList<object> Schemas =>
    [
        new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object>
            {
                ["name"] = ListRecentPosts,
                ["description"] =
                    "List recent articles from the configured publisher feeds, newest first, already split into " +
                    "those inside the freshness window and those older than it. Call this first, and call it " +
                    "again with different keywords or a different source to widen your search. Available " +
                    $"sources: {string.Join(", ", settings.ResearchFeeds.Select(feed => feed.Name))}.",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["source"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional. One source name to list. Omit to list every source.",
                        },
                        ["query"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] =
                                "Optional. Space-separated keywords; an article matches if its title or summary " +
                                "contains any of them. Omit to see everything.",
                        },
                        ["include_older"] = new Dictionary<string, object>
                        {
                            ["type"] = "boolean",
                            ["description"] =
                                "Optional, default false. Also list articles older than the freshness window, " +
                                "which are useful as background context but never as a lead story.",
                        },
                    },
                    ["required"] = Array.Empty<string>(),
                },
            },
        },
        new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object>
            {
                ["name"] = FetchPage,
                ["description"] =
                    "Fetch one article and return its text. Use it on anything you intend to write about — a feed " +
                    "summary is not enough to describe what shipped. The url must be one you saw in a " +
                    $"{ListRecentPosts} result.",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["url"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The article URL, copied exactly from a feed listing.",
                        },
                    },
                    ["required"] = (string[])["url"],
                },
            },
        },
    ];

    public async Task<string> InvokeAsync(string name, string argumentsJson, CancellationToken ct = default)
    {
        var arguments = ParseArguments(argumentsJson);

        return name switch
        {
            ListRecentPosts => await ListRecentPostsAsync(
                ReadString(arguments, "source"),
                ReadString(arguments, "query"),
                ReadBool(arguments, "include_older"),
                ct),
            FetchPage => await FetchPageAsync(ReadString(arguments, "url"), ct),
            _ => $"There is no tool named '{name}'. Available tools: {ListRecentPosts}, {FetchPage}.",
        };
    }

    private async Task<string> ListRecentPostsAsync(string? source, string? query, bool includeOlder, CancellationToken ct)
    {
        var feeds = settings.ResearchFeeds.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(source))
        {
            var requested = source.Trim();
            var matches = settings.ResearchFeeds
                .Where(feed => feed.Name.Equals(requested, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                return $"There is no source named '{requested}'. Available sources: " +
                       $"{string.Join(", ", settings.ResearchFeeds.Select(feed => feed.Name))}.";
            }

            feeds = matches;
        }

        var items = new List<FeedItem>();
        var failures = new List<string>();
        foreach (var feed in feeds)
        {
            try
            {
                items.AddRange(await LoadAsync(feed, ct));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                // One publisher being down or rate-limiting is routine. Say so in the result so the
                // model knows that source was not searched, and carry on with the rest.
                failures.Add($"{feed.Name} ({ex.Message})");
            }
        }

        var formatted = Format(Filter(items, query), today, recentStart, includeOlder);
        if (failures.Count > 0)
            formatted += $"\n\nCould not read: {string.Join("; ", failures)}.";

        return formatted;
    }

    private async Task<string> FetchPageAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "fetch_page needs a url argument.";

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return $"'{url}' is not an absolute http(s) URL. Copy a url exactly as a feed listing printed it.";

        if (!IsAllowed(uri, settings, seenUrls))
        {
            return $"{uri.Host} is not a source this blog draws from, so it cannot be fetched. Pick a url from a " +
                   $"{ListRecentPosts} result instead.";
        }

        try
        {
            var html = await httpClient.GetStringAsync(uri, ct);
            var text = HtmlText.Truncate(HtmlText.ToPlainText(html), settings.ResearchPageMaxChars);

            return text.Length == 0
                ? $"{uri} returned no readable text. Rely on the feed summary, or pick a different article."
                : $"Article: {uri}\n\n{text}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"Could not fetch {uri}: {ex.Message}. Try another article.";
        }
    }

    private async Task<IReadOnlyList<FeedItem>> LoadAsync(FeedSource feed, CancellationToken ct)
    {
        if (cache.TryGetValue(feed.Url, out var cached))
            return cached;

        var xml = await httpClient.GetStringAsync(feed.Url, ct);
        var items = FeedParser.Parse(feed.Name, xml)
            .OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue)
            .Take(settings.ResearchFeedItemsPerSource)
            .ToList();

        foreach (var item in items)
            seenUrls.Add(item.Url);

        cache[feed.Url] = items;
        return items;
    }

    /// <summary>
    /// Keeps items matching any whitespace-separated keyword in <paramref name="query"/>, matching
    /// against title and summary. An empty query keeps everything.
    /// </summary>
    internal static IReadOnlyList<FeedItem> Filter(IEnumerable<FeedItem> items, string? query)
    {
        var terms = (query ?? "").Split((char[])[' ', ',', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return items.ToList();

        return items
            .Where(item => terms.Any(term =>
                item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Renders the listing the model reads, with the freshness split already decided.
    /// </summary>
    internal static string Format(
        IReadOnlyList<FeedItem> items,
        DateOnly today,
        DateOnly recentStart,
        bool includeOlder)
    {
        var ordered = items
            .OrderByDescending(item => item.Published ?? DateTimeOffset.MinValue)
            .ToList();

        var inWindow = ordered.Where(item => IsInWindow(item, recentStart, today)).ToList();
        var older = ordered.Except(inWindow).ToList();

        var text = new StringBuilder();
        text.AppendLine($"Today is {today:yyyy-MM-dd}. The freshness window is {recentStart:yyyy-MM-dd} to {today:yyyy-MM-dd}.");
        text.AppendLine();

        text.AppendLine($"IN WINDOW ({inWindow.Count}) — only these may lead a news post:");
        text.AppendLine(inWindow.Count == 0
            ? "  (nothing published in the window matched)"
            : string.Join("\n", inWindow.Select(Describe)));

        if (includeOlder)
        {
            text.AppendLine();
            text.AppendLine($"OLDER ({older.Count}) — background only, never presented as fresh:");
            text.AppendLine(older.Count == 0
                ? "  (none)"
                : string.Join("\n", older.Select(Describe)));
        }
        else if (older.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"({older.Count} older articles not shown — call again with include_older to see them.)");
        }

        return text.ToString().TrimEnd();
    }

    private static bool IsInWindow(FeedItem item, DateOnly recentStart, DateOnly today)
    {
        if (item.Published is not { } published)
            return false;

        var day = DateOnly.FromDateTime(published.UtcDateTime);
        return day >= recentStart && day <= today;
    }

    private static string Describe(FeedItem item)
    {
        var date = item.Published is { } published
            ? published.UtcDateTime.ToString("yyyy-MM-dd")
            : "undated";

        var summary = HtmlText.Truncate(item.Summary, 400);
        var lines = new List<string>
        {
            $"- {date} | {item.Source} | {item.Title}",
            $"  {item.Url}",
        };
        if (summary.Length > 0)
            lines.Add($"  {summary.ReplaceLineEndings(" ")}");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Whether the run may fetch <paramref name="url"/>. Three ways to qualify, narrowest first: a
    /// feed actually listed this exact URL, the host is one of the configured feeds', or the host
    /// is on the blog's allowed-domain list. The model can only print URLs its dossier contains, so
    /// this is what keeps the dossier itself inside the sources the blog stands behind.
    /// </summary>
    internal static bool IsAllowed(Uri url, GenerationSettings settings, IReadOnlySet<string> seenUrls)
    {
        if (settings.BlockedDomains.Any(domain => HostMatches(url.Host, domain)))
            return false;

        if (seenUrls.Contains(url.ToString()))
            return true;

        var feedHosts = settings.ResearchFeeds
            .Select(feed => Uri.TryCreate(feed.Url, UriKind.Absolute, out var feedUri) ? feedUri.Host : null)
            .Where(host => host is not null)
            .Select(host => host!);

        return feedHosts.Any(host => HostMatches(url.Host, host)) ||
               settings.AllowedDomains.Any(domain => HostMatches(url.Host, domain));
    }

    // "www.infoq.com" matches "infoq.com"; "notinfoq.com" must not.
    private static bool HostMatches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, JsonElement> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> arguments, string name) =>
        arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBool(Dictionary<string, JsonElement> arguments, string name) =>
        arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;

    private const string ListRecentPosts = "list_recent_posts";
    private const string FetchPage = "fetch_page";
}
