using System.Net;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Memes;
using BlogGenerator.Core.PostGeneration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Providers;
using BlogGenerator.Core.Providers.Anthropic;
using BlogGenerator.Core.Providers.AzureFoundry;
using BlogGenerator.Core.Providers.Local;
using BlogGenerator.Core.Providers.Venice;
using BlogGenerator.Core.Research;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Determine repo root: walk up from the executable to find _posts/
var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
    ?? FindRepoRoot(Directory.GetCurrentDirectory())
    ?? throw new InvalidOperationException("Could not find repo root (directory containing _posts/)");

// Secrets come from the environment. Locally a gitignored .env at the repo root is the
// convenient place to keep them; real environment variables always win over its contents.
LoadDotEnv(Path.Combine(repoRoot, ".env"));

// Load non-secret settings from the single application settings file.
var settings = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build()
    .GetRequiredSection("Generation")
    .Get<GenerationSettings>()
    ?? throw new InvalidOperationException("The Generation section is missing from appsettings.json.");
settings.Normalize();
settings.Validate();
settings.RepoRoot = repoRoot;

// The local provider has no provider-side web search. By default it researches the configured
// feeds itself first; --dossier supplies a better one gathered elsewhere, and --no-research skips
// the stage entirely for an evergreen post with no source links.
settings.LocalDossierPath = ReadDossierOption(args);
settings.ResearchDisabled = args.Any(arg => arg.Equals("--no-research", StringComparison.OrdinalIgnoreCase));

var services = new ServiceCollection();
services.AddSingleton(settings);
services.AddHttpClient<AnthropicProvider>();
services.AddSingleton<AzureFoundryProvider>();
services.AddHttpClient<VeniceProvider>(client => client.Timeout = TimeSpan.FromMinutes(5));
services.AddHttpClient<LocalProvider>(client =>
    client.Timeout = TimeSpan.FromMinutes(settings.LocalTimeoutMinutes));
// Feeds and article pages are ordinary web requests: a minute is generous, and a slow publisher
// should not inherit the model's half-hour patience.
services.AddHttpClient<FeedResearchTools>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(1);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ai-dotnet-autopost-blog/1.0 (+feed research)");
})
// Not an optimization. Some publishers return a gzip body whether or not it was negotiated, and
// without this the feed arrives as binary and fails to parse — which reads as a malformed feed.
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
});
services.AddHttpClient<ImgflipClient>();
using var provider = services.BuildServiceProvider();

// Determine provider from CLI arg or env var
var providerName = args.Length > 0 && !args[0].StartsWith('-')
    ? args[0]
    : Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "anthropic";

IAIProvider aiProvider = providerName.ToLowerInvariant() switch
{
    "anthropic" or "claude" => provider.GetRequiredService<AnthropicProvider>(),
    "foundry" or "azure" => provider.GetRequiredService<AzureFoundryProvider>(),
    "venice" => provider.GetRequiredService<VeniceProvider>(),
    "local" => provider.GetRequiredService<LocalProvider>(),
    _ => throw new ArgumentException(
        $"Unknown AI provider: {providerName}. Use 'anthropic', 'foundry', 'venice', or 'local'."),
};

Console.WriteLine($"Using provider: {aiProvider.ProviderName}");
Console.WriteLine($"Repo root: {repoRoot}");

// What we already published is the only cross-run state the generator has. Without it every run
// researches the same fixed angles with no idea the last one did too.
var recentPosts = PublishedHistory.Read(repoRoot, settings.RecentPostHistoryCount);
if (recentPosts.Count > 0)
    Console.WriteLine($"Avoiding the topics of the last {recentPosts.Count} posts (newest: {recentPosts[0].Title}).");

var promptContext = PromptBuilder.Build(settings, recentPosts: recentPosts);
var response = await aiProvider.GeneratePostAsync(promptContext, settings);

var imgflipClient = settings.ImgflipMemeEnabled
    ? provider.GetRequiredService<ImgflipClient>()
    : null;

var (postPath, memeRelPath) = PostWriter.WritePost(
    response.Markdown,
    settings,
    usedModels: response.UsedModels,
    imgflipClient: imgflipClient,
    extraTags: response.ExtraTags);
Console.WriteLine($"Post generated: {postPath}");
if (memeRelPath != null)
    Console.WriteLine($"Meme generated: {memeRelPath}");

// --dossier <path>: the research brief the local provider composes the post from.
static string? ReadDossierOption(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].Equals("--dossier", StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            throw new ArgumentException("--dossier requires a path to the research dossier file.");

        return args[i + 1];
    }

    return null;
}

// Minimal KEY=VALUE reader so `dotnet run` works locally without exporting anything first.
// Deliberately does not overwrite variables that are already set, so CI secrets take priority.
static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
        return;

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            continue;

        var separator = line.IndexOf('=');
        if (separator <= 0)
            continue;

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim().Trim('"', '\'');

        if (key.Length > 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}

static string? FindRepoRoot(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "_posts")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}
