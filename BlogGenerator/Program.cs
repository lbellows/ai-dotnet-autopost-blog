using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Memes;
using BlogGenerator.Core.PostGeneration;
using BlogGenerator.Core.Prompts;
using BlogGenerator.Core.Providers;
using BlogGenerator.Core.Providers.Anthropic;
using BlogGenerator.Core.Providers.AzureFoundry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Determine repo root: walk up from the executable to find _posts/
var repoRoot = FindRepoRoot(AppContext.BaseDirectory)
    ?? FindRepoRoot(Directory.GetCurrentDirectory())
    ?? throw new InvalidOperationException("Could not find repo root (directory containing _posts/)");

// Load non-secret settings from the single application settings file.
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

// Register HttpClient for providers
builder.Services.AddHttpClient<AnthropicProvider>();
builder.Services.AddSingleton<AzureFoundryProvider>();
builder.Services.AddHttpClient<ImgflipClient>();

var host = builder.Build();

// Determine provider from CLI arg or env var
var providerName = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "anthropic";

var settings = configuration.GetRequiredSection("Generation").Get<GenerationSettings>()
    ?? throw new InvalidOperationException("The Generation section is missing from appsettings.json.");
settings.Normalize();
settings.Validate();
settings.RepoRoot = repoRoot;

IAIProvider provider = providerName.ToLowerInvariant() switch
{
    "anthropic" or "claude" => host.Services.GetRequiredService<AnthropicProvider>(),
    "foundry" or "azure" => host.Services.GetRequiredService<AzureFoundryProvider>(),
    _ => throw new ArgumentException($"Unknown AI provider: {providerName}. Use 'anthropic' or 'foundry'."),
};

Console.WriteLine($"Using provider: {provider.ProviderName}");
Console.WriteLine($"Repo root: {repoRoot}");

var promptContext = PromptBuilder.Build(settings);
var response = await provider.GeneratePostAsync(promptContext, settings);

var imgflipClient = settings.ImgflipMemeEnabled
    ? host.Services.GetRequiredService<ImgflipClient>()
    : null;

var (postPath, memeRelPath) = PostWriter.WritePost(response.Markdown, settings, usedModel: response.UsedModel, imgflipClient: imgflipClient);
Console.WriteLine($"Post generated: {postPath}");
if (memeRelPath != null)
    Console.WriteLine($"Meme generated: {memeRelPath}");

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
