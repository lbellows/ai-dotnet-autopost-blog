using BlogGenerator.Core.Research;

namespace BlogGenerator.Tests;

public class DossierArchiveTests
{
    [Fact]
    public async Task SavesIntoTheConfiguredDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dossier-test-{Guid.NewGuid():N}");
        var previous = Environment.GetEnvironmentVariable(DossierArchive.DirectoryVariable);
        Environment.SetEnvironmentVariable(DossierArchive.DirectoryVariable, directory);
        try
        {
            var path = await DossierArchive.SaveAsync("## In-window findings\nNone.", new DateOnly(2026, 9, 25));

            Assert.Equal(directory, Path.GetDirectoryName(path));
            Assert.StartsWith("blog-dossier-2026-09-25-", Path.GetFileName(path));
            Assert.Equal("## In-window findings\nNone.", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable(DossierArchive.DirectoryVariable, previous);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
