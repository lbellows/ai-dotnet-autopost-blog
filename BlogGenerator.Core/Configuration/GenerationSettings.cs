namespace BlogGenerator.Core.Configuration;

public sealed class GenerationSettings
{
    public string TopicHint { get; set; } = string.Empty;
    public string? TopicUrl { get; set; }
    public int PostWordsMin { get; set; }
    public int PostWordsMax { get; set; }
    public int MaxSearches { get; set; }
    public int RecentWindowDays { get; set; }

    // How many already-published posts the prompts are shown so a run does not repeat the last
    // one. Set to 0 to disable. Long enough to cover a few weeks of a Tue/Thu/Sun cadence; much
    // longer and the list starts crowding the prompt with posts nobody would repeat anyway.
    public int RecentPostHistoryCount { get; set; }

    public List<string> AllowedDomains { get; set; } = [];

    public List<string> BlockedDomains { get; set; } = [];

    public string RepoRoot { get; set; } = string.Empty;
    public string DefaultAuthor { get; set; } = string.Empty;

    // Anthropic
    public string AnthropicModel { get; set; } = string.Empty;
    public int AnthropicMaxTokens { get; set; }
    public double? AnthropicTemperature { get; set; }

    // Azure Foundry
    public List<string> FoundryModels { get; set; } = [];
    public string FoundryDefaultModel { get; set; } = string.Empty;
    public int FoundryMaxTokens { get; set; }
    public double? FoundryTemperature { get; set; }
    public double? FoundryTopP { get; set; }

    // Venice (OpenAI-compatible chat completions with provider-side web search).
    // The brain model researches with web search on; the writer model turns that dossier
    // into the post with search off. Leave VeniceWriterModel empty to run a single call.
    public string VeniceBrainModel { get; set; } = string.Empty;
    public List<string> VeniceBrainFallbackModels { get; set; } = [];
    public string VeniceWriterModel { get; set; } = string.Empty;
    public List<string> VeniceWriterFallbackModels { get; set; } = [];
    public int VeniceResearchMaxTokens { get; set; }
    public int VeniceMaxTokens { get; set; }
    public double? VeniceResearchTemperature { get; set; }
    public double? VeniceTemperature { get; set; }
    public double? VeniceTopP { get; set; }

    // Local OpenAI-compatible server (llama.cpp / llama-swap / Ollama / vLLM). The server address
    // and the model name are deployment facts rather than content defaults, so they come from the
    // environment (LOCAL_AI_BASE_URL / LOCAL_AI_MODEL) and only the tunables live here.
    public int LocalMaxTokens { get; set; }
    public double? LocalTemperature { get; set; }
    public double? LocalTopP { get; set; }

    // Generous by hosted-API standards on purpose: a cold llama-swap evicts the resident model and
    // pages new weights onto the GPUs before the first token, and local decode is slower.
    public int LocalTimeoutMinutes { get; set; }

    // The feed-reading research stage the local provider runs before it writes. Same model, same
    // llama-swap slot — it is a first pass in the same run, not a second model to load.
    public int LocalResearchMaxTokens { get; set; }
    public double? LocalResearchTemperature { get; set; }

    // How many assistant turns the research stage may spend calling tools before it is told to
    // write the dossier with what it has. One turn can carry several tool calls.
    public int LocalResearchMaxRounds { get; set; }

    // Path to the research dossier the local provider writes from, set per run from --dossier.
    // Not configuration: it names one run's scratch file, like RepoRoot names the checkout.
    public string? LocalDossierPath { get; set; }

    // Set per run from --no-research: skip the feed stage and write an evergreen post instead.
    public bool ResearchDisabled { get; set; }

    // Feeds the research stage reads. This is the whole research surface on purpose: no search
    // API, no key, no crawl — a fixed list of publisher feeds whose items are already dated, which
    // is exactly what a freshness window needs.
    public List<FeedSource> ResearchFeeds { get; set; } = [];

    // Per-feed cap on items handed to the model. Some publishers serve their entire archive.
    public int ResearchFeedItemsPerSource { get; set; }

    // Cap on the text of one fetched article, so a long page cannot crowd out the rest of the run.
    public int ResearchPageMaxChars { get; set; }

    public bool ImgflipMemeEnabled { get; set; }

    // One substantial, verifiable code sample per post. Disable to have posts explain
    // implementation details in prose instead. See PromptBuilder.CodeGuidance.
    public bool CodeSamplesEnabled { get; set; } = true;
    public int CodeSampleMinLines { get; set; }
    public int CodeSampleMaxLines { get; set; }

    public void Normalize()
    {
        // Domains are compared and sent lower-cased; model names keep the casing the API expects.
        AllowedDomains = Clean(AllowedDomains, lowercase: true);
        BlockedDomains = Clean(BlockedDomains, lowercase: true);
        FoundryModels = Clean(FoundryModels);
        VeniceBrainFallbackModels = Clean(VeniceBrainFallbackModels);
        VeniceWriterFallbackModels = Clean(VeniceWriterFallbackModels);

        // A half-filled feed entry is a config typo, and dropping it beats failing the whole run
        // over one bad row. Names are lower-cased because the model is asked to type them back.
        ResearchFeeds = ResearchFeeds
            .Where(feed => !string.IsNullOrWhiteSpace(feed.Name) && !string.IsNullOrWhiteSpace(feed.Url))
            .Select(feed => new FeedSource { Name = feed.Name.Trim().ToLowerInvariant(), Url = feed.Url.Trim() })
            .DistinctBy(feed => feed.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> Clean(IEnumerable<string> values, bool lowercase = false) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => lowercase ? value.Trim().ToLowerInvariant() : value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void Validate()
    {
        Require(!string.IsNullOrWhiteSpace(TopicHint), "TopicHint must be set");
        Require(PostWordsMin > 0, "PostWordsMin must be greater than 0");
        Require(PostWordsMax >= PostWordsMin, "PostWordsMax must be greater than or equal to PostWordsMin");
        Require(MaxSearches > 0, "MaxSearches must be greater than 0");
        Require(RecentWindowDays > 0, "RecentWindowDays must be greater than 0");
        Require(RecentPostHistoryCount >= 0, "RecentPostHistoryCount must be 0 or greater");
        Require(!string.IsNullOrWhiteSpace(DefaultAuthor), "DefaultAuthor must be set");
        Require(!string.IsNullOrWhiteSpace(AnthropicModel), "AnthropicModel must be set");
        Require(AnthropicMaxTokens > 0, "AnthropicMaxTokens must be greater than 0");
        Require(FoundryMaxTokens > 0, "FoundryMaxTokens must be greater than 0");
        Require(
            FoundryModels.Count > 0 || !string.IsNullOrWhiteSpace(FoundryDefaultModel),
            "FoundryModels or Generation:FoundryDefaultModel must be set");
        Require(!string.IsNullOrWhiteSpace(VeniceBrainModel), "VeniceBrainModel must be set");
        Require(VeniceResearchMaxTokens > 0, "VeniceResearchMaxTokens must be greater than 0");
        Require(VeniceMaxTokens > 0, "VeniceMaxTokens must be greater than 0");
        Require(LocalMaxTokens > 0, "LocalMaxTokens must be greater than 0");
        Require(LocalTimeoutMinutes > 0, "LocalTimeoutMinutes must be greater than 0");
        Require(LocalResearchMaxTokens > 0, "LocalResearchMaxTokens must be greater than 0");
        Require(LocalResearchMaxRounds > 0, "LocalResearchMaxRounds must be greater than 0");
        if (ResearchFeeds.Count > 0)
        {
            Require(
                ResearchFeedItemsPerSource > 0,
                "ResearchFeedItemsPerSource must be greater than 0 when ResearchFeeds are configured");
            Require(
                ResearchPageMaxChars > 0,
                "ResearchPageMaxChars must be greater than 0 when ResearchFeeds are configured");
            foreach (var feed in ResearchFeeds)
            {
                Require(
                    Uri.TryCreate(feed.Url, UriKind.Absolute, out var feedUri) &&
                        feedUri.Scheme is "http" or "https",
                    $"ResearchFeeds entry '{feed.Name}' must have an absolute http(s) Url");
            }
        }
        if (CodeSamplesEnabled)
        {
            Require(CodeSampleMinLines > 0, "CodeSampleMinLines must be greater than 0");
            Require(
                CodeSampleMaxLines >= CodeSampleMinLines,
                "CodeSampleMaxLines must be greater than or equal to CodeSampleMinLines");
        }
    }

    private static void Require(bool condition, string requirement)
    {
        if (!condition)
            throw new InvalidOperationException($"Generation:{requirement} in appsettings.json.");
    }
}
