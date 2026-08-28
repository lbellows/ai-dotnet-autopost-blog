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

    public void Normalize()
    {
        AllowedDomains = NormalizeDomains(AllowedDomains);
        BlockedDomains = NormalizeDomains(BlockedDomains);
        FoundryModels = NormalizeValues(FoundryModels, StringComparer.OrdinalIgnoreCase);
        VeniceBrainFallbackModels = NormalizeValues(VeniceBrainFallbackModels, StringComparer.OrdinalIgnoreCase);
        VeniceWriterFallbackModels = NormalizeValues(VeniceWriterFallbackModels, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> NormalizeDomains(IEnumerable<string> domains) =>
        domains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeValues(IEnumerable<string> values, StringComparer comparer) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(comparer)
            .ToList();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TopicHint))
            throw new InvalidOperationException("Generation:TopicHint must be set in appsettings.json.");
        if (PostWordsMin <= 0)
            throw new InvalidOperationException("Generation:PostWordsMin must be greater than 0 in appsettings.json.");
        if (PostWordsMax < PostWordsMin)
            throw new InvalidOperationException("Generation:PostWordsMax must be greater than or equal to PostWordsMin in appsettings.json.");
        if (MaxSearches <= 0)
            throw new InvalidOperationException("Generation:MaxSearches must be greater than 0 in appsettings.json.");
        if (RecentWindowDays <= 0)
            throw new InvalidOperationException("Generation:RecentWindowDays must be greater than 0 in appsettings.json.");
        if (string.IsNullOrWhiteSpace(DefaultAuthor))
            throw new InvalidOperationException("Generation:DefaultAuthor must be set in appsettings.json.");
        if (string.IsNullOrWhiteSpace(AnthropicModel))
            throw new InvalidOperationException("Generation:AnthropicModel must be set in appsettings.json.");
        if (AnthropicMaxTokens <= 0)
            throw new InvalidOperationException("Generation:AnthropicMaxTokens must be greater than 0 in appsettings.json.");
        if (FoundryMaxTokens <= 0)
            throw new InvalidOperationException("Generation:FoundryMaxTokens must be greater than 0 in appsettings.json.");
        if (FoundryModels.Count == 0 && string.IsNullOrWhiteSpace(FoundryDefaultModel))
            throw new InvalidOperationException("Generation:FoundryModels or Generation:FoundryDefaultModel must be set in appsettings.json.");
        if (string.IsNullOrWhiteSpace(VeniceBrainModel))
            throw new InvalidOperationException("Generation:VeniceBrainModel must be set in appsettings.json.");
        if (VeniceResearchMaxTokens <= 0)
            throw new InvalidOperationException("Generation:VeniceResearchMaxTokens must be greater than 0 in appsettings.json.");
        if (VeniceMaxTokens <= 0)
            throw new InvalidOperationException("Generation:VeniceMaxTokens must be greater than 0 in appsettings.json.");
    }
}
