namespace BlogGenerator.Core.Research;

/// <summary>
/// One entry from a syndication feed, normalized across RSS and Atom.
/// </summary>
/// <param name="Source">The configured feed name, e.g. "devblogs".</param>
/// <param name="Title">The entry title, plain text.</param>
/// <param name="Url">The canonical link to the entry.</param>
/// <param name="Published">When the entry says it was published, or null when it does not say.</param>
/// <param name="Summary">The entry's own summary or description, stripped of markup.</param>
public sealed record FeedItem(
    string Source,
    string Title,
    string Url,
    DateTimeOffset? Published,
    string Summary);
