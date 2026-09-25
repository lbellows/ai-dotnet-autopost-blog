namespace BlogGenerator.Core.Research;

/// <summary>
/// Keeps the research dossier a post was written from, so a wrong claim in the post can be traced
/// to the stage that introduced it: was it in the research, or did the writer add it?
/// </summary>
public static class DossierArchive
{
    // The scheduled workflow sets this and uploads the directory as a run artifact.
    public const string DirectoryVariable = "DOSSIER_DIR";

    public static async Task<string> SaveAsync(string dossier, DateOnly today, CancellationToken ct = default)
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.GetTempPath();
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"blog-dossier-{today:yyyy-MM-dd}-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(path, dossier, ct);
        return path;
    }
}
