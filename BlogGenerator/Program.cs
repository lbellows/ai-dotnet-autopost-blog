using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Memes;
using BlogGenerator.Core.PostGeneration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Providers;
using BlogGenerator.Core.Providers.Anthropic;
using BlogGenerator.Core.Providers.AzureFoundry;
using BlogGenerator.Core.Providers.Venice;
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

var services = new ServiceCollection();
services.AddHttpClient<AnthropicProvider>();
services.AddSingleton<AzureFoundryProvider>();
services.AddHttpClient<VeniceProvider>(client => client.Timeout = TimeSpan.FromMinutes(5));
services.AddHttpClient<ImgflipClient>();
using var provider = services.BuildServiceProvider();

// Determine provider from CLI arg or env var
var providerName = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "anthropic";

IAIProvider aiProvider = providerName.ToLowerInvariant() switch
{
    "anthropic" or "claude" => provider.GetRequiredService<AnthropicProvider>(),
    "foundry" or "azure" => provider.GetRequiredService<AzureFoundryProvider>(),
    "venice" => provider.GetRequiredService<VeniceProvider>(),
    _ => throw new ArgumentException($"Unknown AI provider: {providerName}. Use 'anthropic', 'foundry', or 'venice'."),
};

Console.WriteLine($"Using provider: {aiProvider.ProviderName}");
Console.WriteLine($"Repo root: {repoRoot}");

var promptContext = PromptBuilder.Build(settings);
var response = await aiProvider.GeneratePostAsync(promptContext, settings);

var imgflipClient = settings.ImgflipMemeEnabled
    ? provider.GetRequiredService<ImgflipClient>()
    : null;

var (postPath, memeRelPath) = PostWriter.WritePost(response.Markdown, settings, usedModels: response.UsedModels, imgflipClient: imgflipClient);
Console.WriteLine($"Post generated: {postPath}");
if (memeRelPath != null)
    Console.WriteLine($"Meme generated: {memeRelPath}");

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
