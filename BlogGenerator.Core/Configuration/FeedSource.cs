namespace BlogGenerator.Core.Configuration;

/// <summary>
/// One syndication feed the research stage may read. <see cref="Name"/> is the short label the
/// model uses to ask for a single source, so keep it recognizable ("dotnet", "github").
/// </summary>
public sealed class FeedSource
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
