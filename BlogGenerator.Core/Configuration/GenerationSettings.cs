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

    public bool ImgflipMemeEnabled { get; set; }

    public void Normalize()
    {
        AllowedDomains = NormalizeDomains(AllowedDomains);
        BlockedDomains = NormalizeDomains(BlockedDomains);
        FoundryModels = NormalizeValues(FoundryModels, StringComparer.OrdinalIgnoreCase);
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
    }
}
