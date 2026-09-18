using BlogGenerator.Core.PostGeneration;

namespace BlogGenerator.Tests;

public class PublishedHistoryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"blog-history-tests-{Guid.NewGuid():N}");

    private string WritePost(string fileName, string? title)
    {
        var dir = Path.Combine(_root, "_posts");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        var frontMatter = title is null
            ? "---\nlayout: post\n---\n\nBody."
            : $"---\nlayout: post\ntitle: \"{title}\"\ndate: 2026-09-15 12:00:00 -0400\ntags: [azure]\nauthor: the.serf\n---\n\nBody.";
        File.WriteAllText(path, frontMatter);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ReadsTitleAndDateNewestFirst()
    {
        WritePost("2026-09-10-older.md", "Older Post");
        WritePost("2026-09-15-newer.md", "Newer Post");

        var posts = PublishedHistory.Read(_root, 10);

        Assert.Equal(2, posts.Count);
        Assert.Equal("Newer Post", posts[0].Title);
        Assert.Equal(new DateOnly(2026, 9, 15), posts[0].Date);
        Assert.Equal("Older Post", posts[1].Title);
        Assert.Equal(new DateOnly(2026, 9, 10), posts[1].Date);
    }

    [Fact]
    public void KeepsOnlyTheRequestedCount()
    {
        WritePost("2026-09-10-a.md", "A");
        WritePost("2026-09-11-b.md", "B");
        WritePost("2026-09-12-c.md", "C");

        var posts = PublishedHistory.Read(_root, 2);

        Assert.Equal(["C", "B"], posts.Select(post => post.Title));
    }

    // The two-posts-in-a-day case PostWriter's "-2" suffix produces must not collapse to one.
    [Fact]
    public void KeepsBothPostsPublishedOnTheSameDay()
    {
        WritePost("2026-09-11-first.md", "First");
        WritePost("2026-09-11-second-2.md", "Second");

        var posts = PublishedHistory.Read(_root, 10);

        Assert.Equal(2, posts.Count);
        Assert.All(posts, post => Assert.Equal(new DateOnly(2026, 9, 11), post.Date));
    }

    [Fact]
    public void UnescapesQuotesInTheFrontMatterTitle()
    {
        WritePost("2026-09-15-quoted.md", "The \\\"Noisy Neighbor\\\" Problem");

        var posts = PublishedHistory.Read(_root, 10);

        Assert.Equal("The \"Noisy Neighbor\" Problem", posts[0].Title);
    }

    [Fact]
    public void FallsBackToTheSlugWhenFrontMatterHasNoTitle()
    {
        WritePost("2026-09-15-ptu-or-pay-as-you-go.md", title: null);

        var posts = PublishedHistory.Read(_root, 10);

        Assert.Equal("Ptu or pay as you go", posts[0].Title);
    }

    [Fact]
    public void IgnoresFilesThatAreNotDatedPosts()
    {
        WritePost("2026-09-15-real.md", "Real");
        WritePost("draft-notes.md", "Draft");

        var posts = PublishedHistory.Read(_root, 10);

        Assert.Equal(["Real"], posts.Select(post => post.Title));
    }

    // A fresh checkout with no archive has nothing to avoid repeating; that is not an error.
    [Fact]
    public void ReturnsEmptyWhenThereIsNoPostsDirectory()
    {
        Assert.Empty(PublishedHistory.Read(_root, 10));
    }

    [Fact]
    public void ReturnsEmptyWhenHistoryIsDisabled()
    {
        WritePost("2026-09-15-real.md", "Real");

        Assert.Empty(PublishedHistory.Read(_root, 0));
    }
}
