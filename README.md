# blog
Tech Thoughts

Trying out some github pages features

link: https://lbellows.github.io/blog/

[Contributor guide →](AGENTS.md)
[Changelog →](CHANGELOG.md)

# About AI blog posting

I needed an easy solution for this because I was holding my daughter, so not too much typing could be involed:

* git workflows
* executes C# (.NET 10) generator
* calls claude API with search tool enabled (or Azure Foundry / Venice.ai as alternate providers)
* writes the blog post in MD & commits
* triggers jekyll build which updates the blog

That is my AI-slop-posting pipeline.  Costs about $0.20 per post (sonnet 4.5). Haiku brings it down to about $0.07. This is mostly exploratory and I hope to test out some other tools/solutions in the future integration with social media.

## Quick start — clone the repo

Open a terminal and run:

```sh
git clone https://github.com/lbellows/blog.git
cd blog
```

## Where the workflow and generator live

- C# solution: `BlogGenerator.sln`
- Console app: `BlogGenerator/Program.cs`
- Core logic: `BlogGenerator.Core/`
- Scheduled workflow: `.github/workflows/daily-post-rag.yml`
- Configuration: `BlogGenerator/appsettings.json`

Provider selection at runtime via CLI arg (`anthropic`, `foundry`, or `venice`) or `AI_PROVIDER` env var.

## Run the generator locally (for testing)

Requires .NET 10 SDK. Run with your Anthropic key exported:

```sh
export ANTHROPIC_API_KEY="sk-..."
dotnet run --project BlogGenerator -- anthropic
```

For Azure Foundry via the Azure OpenAI-compatible Responses endpoint:

```sh
export FOUNDRY_OPENAI_ENDPOINT="https://...openai.azure.com/openai/v1/"
export FOUNDRY_PROJECT_API_KEY="..."
dotnet run --project BlogGenerator -- foundry
```

For Venice.ai (OpenAI-compatible chat completions with provider-side web search):

```sh
export VENICE_API_KEY="..."
dotnet run --project BlogGenerator -- venice
```

Instead of exporting anything, you can copy `.env.example` to `.env` and fill it in — the
generator reads that file at startup. `.env` is gitignored; real environment variables always
win over its contents, so CI secrets are unaffected.

## Run the tests

```sh
dotnet test BlogGenerator.sln
```

## Content defaults (adjust in `appsettings.json`)

`BlogGenerator/appsettings.json` is the only non-secret configuration source. `GenerationSettings.cs` now only defines the typed shape plus validation; it does not carry fallback model or content defaults.

- `TopicHint` — short instruction describing audience/angle (example: "AI + .NET + Azure + GitHub + LLM").
- `MaxSearches` — maximum number of web-search calls the model may perform (integer).
- `AllowedDomains` — domains to bias results toward (optional). Defaults include Microsoft/GitHub properties plus tech press (`learn.microsoft.com`, `azure.microsoft.com`, `techcommunity.microsoft.com`, `blogs.microsoft.com`, `devblogs.microsoft.com`, `developer.microsoft.com`, `github.blog`, `techcrunch.com`, `venturebeat.com`, `infoq.com`).
- `BlockedDomains` — domains to avoid (optional).
- `PostWordsMin` — minimum desired words in the generated post.
- `PostWordsMax` — maximum desired words in the generated post.
- `RecentWindowDays` — how many days back the web search should look when hunting for breaking news (defaults to `2`).
- `TopicUrl` — optional primary link to anchor the article around.
- `DefaultAuthor` — default author name injected into front matter.
- `AnthropicModel` — default Claude deployment slug.
- `AnthropicMaxTokens` / `AnthropicTemperature` — controls Claude response length and creativity.
- `FoundryModels` — ordered list of Azure Foundry deployments the Responses path can try after the configured default deployment.
- `FoundryDefaultModel`/`FoundryMaxTokens`/`FoundryTemperature`/`FoundryTopP` — generation parameters used by the Foundry path. `FoundryDefaultModel` is always tried first.
- `VeniceBrainModel` / `VeniceBrainFallbackModels` — the research ("brain") model that runs the grounded web-search passes, plus ordered fallbacks tried when it errors.
- `VeniceWriterModel` / `VeniceWriterFallbackModels` — the model that writes the post from the research dossier. Leave `VeniceWriterModel` empty to collapse Venice into a single search-and-write call.
- `VeniceResearchMaxTokens` / `VeniceResearchTemperature` — budget and creativity for each research pass (kept low so the brain stays factual).
- `VeniceMaxTokens` / `VeniceTemperature` / `VeniceTopP` — generation parameters for the writing pass.
- `MemeGuidanceEnabled` — toggles whether prompts instruct the model to embed a meme image.
- Generated posts automatically add a model tag (e.g., `claude-sonnet-4-6`) so you can filter by source model.

Azure Foundry generation now uses the Azure OpenAI-compatible Responses API with API-key auth. The Foundry path hits your configured `FOUNDRY_OPENAI_ENDPOINT`, tries the configured model list in order, and forces Azure web search via the preview Responses web-search tool. The prompt also biases source selection toward your configured `AllowedDomains` list.

`DeepSeek-V3.2` was removed from the default Foundry model list because Microsoft documents it as not supporting tool calling, which makes it a poor fit for grounded web-search generation.

### Venice: two models, because its search is single-shot

Venice grounds requests with a provider-side web search toggled through `venice_parameters`, not
with an agentic tool loop the model can call repeatedly. One request buys one retrieval pass, so
the provider splits the work in two:

1. **Brain (`grok-4-6`)** — runs one grounded research call per angle (vendor engineering blogs,
   official changelogs and release notes, GitHub releases, developer news coverage), which is how
   the "at least 4 distinct search attempts" requirement is honored on this provider. Each pass
   returns a factual brief that separates in-window findings from older context, and Venice's
   citation payload supplies verbatim source URLs. `grok-4-6` was picked because it was the most
   disciplined of the candidates tested about *dates* — correctly refusing to pass an older
   release off as this week's news, which is the decision that drives NEWS vs. EVERGREEN mode.
2. **Writer (`claude-sonnet-5`)** — composes the post from the merged dossier with search off. It
   keeps the blog's established voice (the Anthropic path uses `claude-sonnet-4-6`) and is both
   stronger and cheaper than `claude-sonnet-4-6` on Venice's price list.

A failed research pass is logged and skipped rather than aborting the run; the remaining passes
still ground the post. Venice models emit superscript citation markers (`^4^`, `^1,5,8^`) inline,
which the provider strips before the post is written to disk.

These are defined in `BlogGenerator/appsettings.json`. Runtime auth/integration values come from environment variables only: `ANTHROPIC_API_KEY`, `FOUNDRY_OPENAI_ENDPOINT`, `FOUNDRY_PROJECT_API_KEY`, and `VENICE_API_KEY`.

Tags are derived automatically from section headings/TL;DR content plus the model name (e.g., `claude`). No manual tag list is required.

## Add the secret to GitHub Actions (alternate: CLI)

You already have the UI instruction above (Repo → Settings → Secrets and variables → Actions). As an alternative you can set the secret using the GitHub CLI:

```sh
# securely set your secret value (recommended: read from environment or file)
gh secret set ANTHROPIC_API_KEY --body "$ANTHROPIC_API_KEY"
```

Note: the workflow expects `ANTHROPIC_API_KEY` in repository secrets.

## Secrets to add (one-time) via UI

Repo → Settings → Secrets and variables → Actions:

ANTHROPIC_API_KEY — from your Anthropic account.

VENICE_API_KEY — from Venice.ai → Settings → API. Only needed if you run the `venice` provider.
Venice bills per request against a prepaid USD/DIEM balance; a zero balance returns HTTP 402.

## Notes & tips

Models with web search: Claude 3.7 Sonnet and newer (plus several others) support this tool; see the docs for the supported list.

Pricing: Web search calls are billed in addition to tokens ($10 / 1,000 searches). Keep MaxSearches low (e.g., 3–6) to control cost.

Citations: Claude will include citations automatically in its response when using web search. Your post will then carry those links in the "Further reading" section the prompt asks for.

Domain control: Set AllowedDomains to bias sources you trust (e.g., arxiv.org, blogs.microsoft.com). BlockedDomains can filter out low-quality sites.

Schedule & publish time: Adjust cron and the front-matter timestamp to your preference.

Manual test: Use the workflow's Run workflow button to test once you add the secret. You can select `anthropic`, `foundry`, or `venice` as the provider.

# Content cadence & tone

- Posts publish Tue & Thu (deep dive on one breaking story from the most recent `RecentWindowDays` window) — see the cron in `.github/workflows/daily-post-rag.yml`.
- Sunday runs switch to a weekly synopsis that blends news and forward-looking tips; the generator detects Sunday automatically.
- Each post highlights at least one of .NET, Azure, or GitHub while keeping a light, professional sense of humor.
- Memes are generated via the imgflip API (`ImgflipMemeEnabled` in `appsettings.json`). The model emits a `<!-- meme: template=..., texts="..." -->` comment choosing one template from a curated catalog; `ImgflipClient` captions that template through imgflip and the rendered image URL is spliced back into the post at the model's chosen spot. Requires `IMGFLIP_USERNAME`/`IMGFLIP_PASSWORD` secrets; if absent or the call fails, the post is published meme-free.
- The meme catalog (`PromptBuilder.ImgflipTemplateCatalog`) is presented to the model in a freshly shuffled order each run so it varies its pick instead of defaulting to one template (it had been over-using "Two Buttons"). To add/remove templates, edit that single list — name and box descriptions live together.

# Future

* check if search tool is getting recent items in azure models
* figure out how to monetize
* revisit automatic meme generation once styling and asset library are settled
* `search.json` is loaded in full on the client (title + excerpt + tags for every post). Fine now (~260 posts); revisit once it grows large — trim per-post payload or move to a prebuilt/lazy index. No storage/build concern: repo and Jekyll build stay well within GitHub Pages limits for years at the current rate.
