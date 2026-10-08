# blog
Tech Thoughts

Trying out some github pages features

link: https://lbellows.github.io/blog/

[Contributor guide →](AGENTS.md)

# About AI blog posting

I needed an easy solution for this because I was holding my daughter, so not too much typing could be involed:

* git workflows
* executes C# (.NET 10) generator
* researches with Venice.ai's web search, then writes from that research (the Anthropic API or a self-hosted model are alternate providers)
* writes the blog post in MD & commits
* triggers jekyll build which updates the blog

That is my AI-slop-posting pipeline. The scheduled Venice run costs about $0.20 per post, almost all of it the Grok research passes; the writer is under a cent. This is mostly exploratory and I hope to test out some other tools/solutions in the future integration with social media.

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

Provider selection at runtime via CLI arg (`anthropic`, `venice`, or `local`) or `AI_PROVIDER` env var.

## Run the generator locally (for testing)

Requires .NET 10 SDK. Run with your Anthropic key exported:

```sh
export ANTHROPIC_API_KEY="sk-..."
dotnet run --project BlogGenerator -- anthropic
```

For Venice.ai (OpenAI-compatible chat completions with provider-side web search):

```sh
export VENICE_API_KEY="..."
dotnet run --project BlogGenerator -- venice
```

For a self-hosted OpenAI-compatible server (llama.cpp, llama-swap, Ollama, vLLM) — free, and
nothing leaves the LAN:

```sh
export LOCAL_AI_BASE_URL="http://main:8080/v1"   # a bare host works too
export LOCAL_AI_MODEL="gemma-26b"                # an id from GET /v1/models
dotnet run --project BlogGenerator -- local
```

That researches the configured feeds and writes from what it finds. Two flags change where the
facts come from: `--dossier <path>` writes from a brief you gathered elsewhere instead, and
`--no-research` skips research entirely for an evergreen post with no source links.

Instead of exporting anything, you can copy `.env.example` to `.env` and fill it in — the
generator reads that file at startup. `.env` is gitignored; real environment variables always
win over its contents, so CI secrets are unaffected.

## Run the tests

```sh
dotnet test BlogGenerator.sln
```

## Content defaults (adjust in `appsettings.json`)

`BlogGenerator/appsettings.json` is the only non-secret configuration source. `GenerationSettings.cs` only defines the typed shape plus validation; it does not carry fallback model or content defaults.

- `TopicHint` — short instruction describing audience/angle (example: "AI + .NET + Azure + GitHub + LLM"). It sets the scope of the research angles, so a topic missing from it is a topic research will not look for.
- `MaxSearches` — caps web searches: Anthropic's `max_uses` on its search tool, and the number of research passes Venice runs (one per angle in `PromptBuilder.ResearchAngles`, which has four).
- `AllowedDomains` — domains to bias results toward (optional). Defaults include Microsoft/GitHub properties plus tech press (`learn.microsoft.com`, `azure.microsoft.com`, `techcommunity.microsoft.com`, `blogs.microsoft.com`, `devblogs.microsoft.com`, `developer.microsoft.com`, `github.blog`, `techcrunch.com`, `venturebeat.com`, `infoq.com`).
- `BlockedDomains` — domains to avoid (optional).
- `PostWordsMin` — minimum desired words in the generated post.
- `PostWordsMax` — maximum desired words in the generated post.
- `RecentWindowDays` — how many days back research looks when hunting for breaking news (currently `2`).
- `RecentPostHistoryCount` — how many already-published posts from `_posts/` the research and writing prompts are shown so a run does not repeat the last one (currently `12`; `0` disables it).
- `TopicUrl` — optional primary link to anchor the article around.
- `DefaultAuthor` — default author name injected into front matter.
- `AnthropicModel` — default Claude deployment slug.
- `AnthropicMaxTokens` / `AnthropicTemperature` — Claude output cap (thinking + article; Sonnet 5.5 thinks by default) and temperature. Leave `AnthropicTemperature` null on Sonnet 5.5, which rejects non-default sampling parameters with a 400.
- `VeniceBrainModel` / `VeniceBrainFallbackModels` — the research ("brain") model that runs the grounded web-search passes, plus ordered fallbacks tried when it errors.
- `VeniceWriterModel` / `VeniceWriterFallbackModels` — the model that writes the post from the research dossier.
- `VeniceResearchMaxTokens` / `VeniceResearchTemperature` — budget and creativity for each research pass (kept low so the brain stays factual).
- `VeniceMaxTokens` / `VeniceTemperature` / `VeniceTopP` — generation parameters for the writing pass.
- `LocalMaxTokens` / `LocalTemperature` / `LocalTopP` — generation parameters for the local server. The address and model name are *not* here: they are deployment facts, so they come from `LOCAL_AI_BASE_URL` / `LOCAL_AI_MODEL`.
- `LocalTimeoutMinutes` — how long to wait on the local server (default `30`). Deliberately generous: a cold llama-swap evicts the resident model and pages new weights onto the GPUs before the first token.
- `LocalResearchMaxTokens` / `LocalResearchTemperature` — budget and creativity for the local research pass (kept low so it stays factual, as with Venice's brain stage).
- `LocalResearchMaxRounds` — how many assistant turns the research pass may spend calling tools before it is told to write the dossier with what it has. One turn can carry several calls.
- `ResearchFeeds` — the `Name`/`Url` list of publisher feeds the local research pass reads. This is its entire research surface: no search API, no key, no crawling.
- `ResearchFeedItemsPerSource` — per-feed cap on items handed to the model. Some publishers serve their whole archive.
- `ResearchPageMaxChars` — cap on the text of one fetched article, so a long page cannot crowd out the rest of the run.
- `ImgflipMemeEnabled` — toggles whether prompts ask the model for a meme and the post writer renders it through imgflip.
- `CodeSamplesEnabled` — toggles whether the prompt asks for a code sample at all. Set to `false` and posts explain implementation details in prose.
- `CodeSampleMinLines` / `CodeSampleMaxLines` — the size band requested for the single code sample (currently `15`/`30`). These also set the threshold `CodeSampleLinter` warns against after generation.

The Azure Foundry provider was retired on 2026-10-07; [docs/retired-foundry-provider.md](docs/retired-foundry-provider.md) describes what it did and how to bring it back.

### Venice: two models, because its search is single-shot

Venice grounds requests with a provider-side web search toggled through `venice_parameters`, not
with an agentic tool loop the model can call repeatedly. One request buys one retrieval pass, so
the provider splits the work in two:

1. **Brain (`grok-4-6`)** — runs one grounded research call per angle (model and platform launches,
   official changelogs and release notes, GitHub releases, developer news coverage), which is how
   the "at least 4 distinct search attempts" requirement is honored on this provider. Each pass
   returns a factual brief that separates in-window findings from older context, and Venice's
   citation payload supplies verbatim source URLs. The brain has to be disciplined about *dates* —
   refusing to pass an older release off as this week's news, which is the decision that drives
   NEWS vs. EVERGREEN mode. Cheaper brains were tried and lost on accuracy; see commit `37877c0`.
2. **Writer (`deepseek-v4-1-flash`)** — composes the post from the merged dossier with search off,
   with thinking disabled: left on, it spent the whole `VeniceMaxTokens` budget reasoning and
   returned no post. About $0.006 a post; `claude-sonnet-5-5` is its first fallback. If the prose
   slips (invented first-person asides, padded "Further reading" lists), set `VeniceWriterModel`
   to `claude-sonnet-5-5`.

A failed research pass is logged and skipped rather than aborting the run; the remaining passes
still ground the post. Venice models emit superscript citation markers (`^4^`, `^1,5,8^`) inline,
which the provider strips before the post is written to disk.

### Local: one model, two passes, and feeds instead of a search API

The `local` provider talks to a self-hosted OpenAI-compatible server. It is single-**model** by
design — a local box has one GPU pool, so Venice's brain/writer split would mean either evicting
the first model or not fitting at all. On `main` that constraint is literal: llama-swap keeps one
model resident on the two 16 GiB cards and swaps to load another.

Single-model is not single-*pass*, though. Research and writing are two passes of the same model
in the same run: no swap, no second load. The first pass gathers facts through tools and writes a
dossier; the second writes the post from that dossier using the same writer prompts as the Venice
writer stage, which forbid printing any URL the dossier does not contain.

There is no provider-side web search behind a self-hosted server, and rather than take a
dependency on a keyed search API, the research pass reads **publisher feeds**. It gets two tools:

- `list_recent_posts` — recent articles across the configured `ResearchFeeds`, filterable by
  source or keyword.
- `fetch_page` — the full text of one of those articles, because a feed summary tells you a thing
  exists but not what changed.

Feeds are a good fit for the actual question this blog asks. "What shipped in the last three
days?" wants a dated list from publishers we already trust, which is exactly what a feed is —
whereas a search API would cost money and hand back an undated relevance ranking. The freshness
split is computed in the tool rather than left to the model, because deciding whether a date is
inside the window is the one thing a small local model should not be asked to do by hand.

Fetching is restricted to URLs a feed actually returned, the feeds' own hosts, and
`AllowedDomains`. A model that invents a URL is told no at the tool boundary instead of having it
discovered in a published post.

Two flags override all of this. `--dossier <path>` writes from a brief gathered elsewhere — an
agent with real web search does better research than ten feeds, so an explicit dossier wins.
`--no-research` skips the stage entirely, which produces an evergreen post with no source links,
because a model with no sources should print no links rather than remembered ones.

Posts from this provider are tagged `local` alongside the model name (`tags: [..., local,
gemma-26b]`), since a model id alone does not say where the post was written.

Runtime auth/integration values come from environment variables only: `ANTHROPIC_API_KEY`, `VENICE_API_KEY`, `LOCAL_AI_BASE_URL` / `LOCAL_AI_MODEL` / `LOCAL_AI_API_KEY`, and `IMGFLIP_USERNAME` / `IMGFLIP_PASSWORD`.

Tags are inferred from the post's headings and salient body terms (fenced code is ignored), plus a tag for every model that wrote it (e.g., `grok-4-6, deepseek-v4-1-flash`). No manual tag list is required.

Every rendered tag is a link to `/tags/?tag=<tag>`, a client-side filtered listing built from `search.json`; `/tags/` with no query lists all tags with their post counts.

## Secrets to add (one-time)

Repo → Settings → Secrets and variables → Actions:

- `VENICE_API_KEY` — from Venice.ai → Settings → API. The scheduled run uses Venice. Venice bills
  per request against a prepaid USD/DIEM balance; a zero balance returns HTTP 402.
- `ANTHROPIC_API_KEY` — from your Anthropic account. Only needed to run the `anthropic` provider.
- `IMGFLIP_USERNAME` / `IMGFLIP_PASSWORD` — optional; without them posts publish meme-free.

Or with the GitHub CLI:

```sh
gh secret set VENICE_API_KEY --body "$VENICE_API_KEY"
```

## Notes & tips

Pricing: on the `anthropic` provider, web search is billed on top of tokens, so `MaxSearches` is the cost knob there.

Citations: sources end up in the "Further reading" section the prompt asks for. On Venice they come from its citation payload, and the writer may only print URLs the dossier contains.

Domain control: Set AllowedDomains to bias sources you trust (e.g., arxiv.org, blogs.microsoft.com). BlockedDomains can filter out low-quality sites.

Schedule & publish time: the cron is in `.github/workflows/daily-post-rag.yml` (fixed UTC); the front-matter timestamp is written in America/New_York by `PostWriter`.

Manual test: Use the workflow's Run workflow button to test once you add the secret. You can select `anthropic` or `venice` as the provider, or leave the menu on `scheduled-default` to run whatever the schedule uses.

### Switching the scheduled provider

The provider is resolved once, in the workflow's `Resolve provider` step:

1. An explicit choice from the `Run workflow` menu overrides everything, for that run only.
2. Otherwise the run uses `DEFAULT_AI_PROVIDER` in `.github/workflows/daily-post-rag.yml` (currently `venice`).

Switching the scheduled provider is that one line — the provider name no longer appears in individual steps.

# Content cadence & tone

- Posts publish Tue & Thu (deep dive on one breaking story from the most recent `RecentWindowDays` window) — see the cron in `.github/workflows/daily-post-rag.yml`.
- Sunday runs switch to a weekly synopsis that blends news and forward-looking tips; the generator detects Sunday automatically.
- Posts cover AI news for software engineers (models and APIs, Azure AI Foundry, AI developer tooling), with .NET and Azure as the readers' home stack, in a light, professional sense of humor.
- Memes are generated via the imgflip API (`ImgflipMemeEnabled` in `appsettings.json`). The model emits a `<!-- meme: template=..., texts="..." -->` comment choosing one template from a curated catalog; `ImgflipClient` captions that template through imgflip and the rendered image URL is spliced back into the post at the model's chosen spot. Requires `IMGFLIP_USERNAME`/`IMGFLIP_PASSWORD` secrets; if absent or the call fails, the post is published meme-free.
- Code samples are capped at one per post and asked to be a substantial, runnable-looking example rather than a fragment (`PromptBuilder.CodeGuidance`). The prompt explicitly prefers no code block over an invented one, and bans pseudocode/conceptual labels, `NotImplementedException`, `...` placeholders, and blocks that are only `dotnet add package`/`azd up` lines. `CodeSampleLinter` re-checks the finished post against those rules and prints warnings to the run log; it never edits or deletes the post, since the pipeline publishes unattended and removing a block would orphan the prose introducing it.
- The meme catalog (`PromptBuilder.ImgflipTemplateCatalog`) is presented to the model in a freshly shuffled order each run so it varies its pick instead of defaulting to one template (it had been over-using "Two Buttons"). To add/remove templates, edit that single list — name and box descriptions live together.
