using System.Globalization;
using System.Xml.Linq;

namespace BlogGenerator.Core.Research;

/// <summary>
/// Reads RSS 2.0 and Atom into <see cref="FeedItem"/>s.
///
/// Elements are matched on local name and never on namespace. Real feeds disagree constantly about
/// namespaces — the same publisher will serve <c>dc:date</c> on one feed and a bare <c>pubDate</c>
/// on another — and a namespace-exact reader silently returns nothing rather than failing loudly,
/// which is the worst way for a research run to break.
/// </summary>
internal static class FeedParser
{
    public static IReadOnlyList<FeedItem> Parse(string source, string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException($"Feed '{source}' did not return well-formed XML: {ex.Message}", ex);
        }

        return document
            .Descendants()
            .Where(element => element.Name.LocalName is "item" or "entry")
            .Select(element => ReadItem(source, element))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private static FeedItem? ReadItem(string source, XElement element)
    {
        var url = ReadLink(element);
        var title = HtmlText.ToPlainText(FirstValue(element, "title"));

        // An entry with no link is useless here: the whole point is handing the writer a URL it is
        // allowed to print.
        if (url.Length == 0 || title.Length == 0)
            return null;

        var summary = HtmlText.ToPlainText(
            FirstValue(element, "description", "summary", "encoded", "content", "subtitle"));

        return new FeedItem(source, title, url, ReadDate(element), summary);
    }

    // Atom carries the URL in an attribute and may list several links (alternate, self, edit);
    // RSS carries it as element text. Try the Atom shape first, then fall back.
    private static string ReadLink(XElement element)
    {
        var links = element.Elements().Where(child => child.Name.LocalName == "link").ToList();

        var alternate = links.FirstOrDefault(link =>
            (string?)link.Attribute("rel") is null or "alternate");

        var href = (string?)alternate?.Attribute("href") ?? (string?)links.FirstOrDefault()?.Attribute("href");
        if (!string.IsNullOrWhiteSpace(href))
            return href.Trim();

        var text = links.Select(link => link.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(text))
            return text.Trim();

        // Some RSS feeds only carry a permalink guid.
        var guid = FirstValue(element, "guid");
        return guid.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? guid : "";
    }

    private static DateTimeOffset? ReadDate(XElement element)
    {
        // "published" before "updated": an Atom entry edited later still announced on its original
        // date, and the freshness window is about the announcement.
        foreach (var name in (string[])["pubDate", "published", "date", "updated", "modified", "created"])
        {
            var raw = FirstValue(element, name);
            if (raw.Length == 0)
                continue;

            if (DateTimeOffset.TryParse(
                    raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string FirstValue(XElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var child = element.Elements()
                .FirstOrDefault(candidate =>
                    candidate.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(candidate.Value));

            if (child is not null)
                return child.Value.Trim();
        }

        return "";
    }
}
