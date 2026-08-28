namespace BlogGenerator.Core.Configuration;

public sealed class GenerationSettings
{
    public string TopicHint { get; set; } = string.Empty;
    public string? TopicUrl { get; set; }
    public int PostWordsMin { get; set; }
    public int PostWordsMax { get; set; }
    public int MaxSearches { get; set; }
    public int RecentWindowDays { get; set; }

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
