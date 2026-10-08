# Repository Guidelines

## Generators & Shared Utilities
The blog generator is a C# .NET 10 solution at the repo root.
- Console app entry point: `BlogGenerator/Program.cs`
- Core logic (providers, prompts, post writing, memes): `BlogGenerator.Core/`
- Unit tests: `BlogGenerator.Tests/`
- Configuration: `BlogGenerator/appsettings.json` (non-secret settings only)
- `appsettings.json` is the single source of truth for non-secret generation settings; `GenerationSettings` only defines the bound schema and validation.
- Provider selection at runtime via CLI arg or `AI_PROVIDER` env var: `anthropic`, `venice`, or `local`. Azure Foundry was retired 2026-10-07 (see `docs/retired-foundry-provider.md`)

## Project Structure & Module Organization
Jekyll powers this GitHub Pages blog. `_config.yml` controls metadata, `_includes/` holds partials, and `index.html` is the landing page. Assets sit in `assets/css/styles.css` and `assets/images/`. Automation lives in the root-level `BlogGenerator`, `BlogGenerator.Core`, and `BlogGenerator.Tests` projects. The generator writes new posts into `_posts/`, and the daily workflow commits them (the directory is tracked in git, not ignored); keep filenames `YYYY-MM-DD-title.md` so Jekyll picks them up.

## Build, Test, and Development Commands
- `dotnet build BlogGenerator.sln` builds the solution.
- `dotnet test BlogGenerator.sln` runs the xUnit suite (providers, prompts, research tools, tags, memes, post output).
- `dotnet run --project BlogGenerator -- anthropic` generates a post using Anthropic Claude; export `ANTHROPIC_API_KEY` first.
- `dotnet run --project BlogGenerator -- venice` generates a post using Venice.ai; set `VENICE_API_KEY` first (or put it in a gitignored `.env`, which the generator loads at startup — see `.env.example`).
- `dotnet run --project BlogGenerator -- local` generates a post with a self-hosted OpenAI-compatible server; set `LOCAL_AI_BASE_URL` and `LOCAL_AI_MODEL` in `.env`. Use the `local-post` skill rather than driving it by hand. `--dossier <path>` writes from a brief gathered elsewhere instead; `--no-research` skips research for an evergreen post.
- The local provider is single-**model**, not single-pass: research and writing are two passes of the same model in one run, so nothing is swapped or loaded twice. Do not add a second local model — one GPU pool holds one model.
- Its research surface is `Generation:ResearchFeeds` and nothing else: publisher feeds plus `fetch_page`, no search API and no key. Keep it that way; adding a keyed search provider reintroduces the spend this provider exists to avoid. Fetching is limited to URLs a feed returned, the feeds' hosts, and `AllowedDomains`.
- The in-window/older split is computed in `FeedResearchTools`, not asked of the model. Local models are unreliable at date arithmetic and this stage exists to answer a date question — leave that decision in code.
- Venice runs two models: a brain model researches with Venice's provider-side web search, then a writer model composes the post from that dossier with search off. Venice's search is a single retrieval pass per request, not an agentic loop, so multiple search angles mean multiple research calls — see `PromptBuilder.ResearchAngles`. Venice and the local provider share the chat-completions request and reply handling in `Providers/ChatCompletions.cs`; each adds only its own request fields.
- `bundle exec jekyll serve --livereload` previews the site locally after installing the `github-pages` gem.
- Default allowed domains bias search toward Microsoft/.NET announcements and reputable tech press; edit `AllowedDomains` in `appsettings.json` if you need changes.
- `PublishedHistory` reads the last `RecentPostHistoryCount` posts out of `_posts/` and both the research and writing prompts are shown that list, because the generator has no other cross-run state: the research angles are fixed, so a thin freshness window used to send every stage back to the same evergreen material. Keep the list going to the research stage too — telling only the writer to avoid a topic while handing it a dossier made of that topic produces a worse post, not a different one.
- Code-sample house rules live in `PromptBuilder.CodeGuidance` and are enforced advisory-only by `CodeSampleLinter` (warnings to the run log, never edits). Keep the sizes in `appsettings.json` (`CodeSampleMinLines`/`CodeSampleMaxLines`), not hardcoded in the prompt.
- Posts must retain the model-name tag (e.g., `claude`) that the generator derives from content. The local provider also adds a `local` tag via `AIProviderResponse.ExtraTags`; provenance tags and model tags are both kept when the tag list is trimmed.
- Scheduled workflow relies on the defaults in `appsettings.json`; avoid reintroducing duplicate tunables into `.github/workflows/daily-post-rag.yml`.
- The workflow resolves the AI provider once, in its `Resolve provider` step: a `workflow_dispatch` menu choice, otherwise the `DEFAULT_AI_PROVIDER` workflow env value. Keep it that way — do not hardcode a provider name into individual steps.

## Coding Style & Naming Conventions
Follow standard C# conventions: PascalCase for public members, camelCase for locals, four-space indentation. Generated front matter should use lowercase tags and minimal quoting. CSS stays in one file—use descriptive classes such as `.post-summary` and cluster overrides by feature.

## Testing Guidelines
After generating a post, review it via the local Jekyll preview and confirm external links resolve. If you need regression fixtures for generated content, keep them with the test project.

## Commit & Pull Request Guidelines
Commit messages stay short and imperative (`clean up`, `drop stale year from Sunday synopsis prompt`), with optional scopes for post runs (`chore(posts): ...`). Keep commits focused and avoid mixing regenerated posts with script changes. Pull requests should summarize publishing impact, link related issues, attach preview screenshots for UI tweaks, and call out new env vars or secrets.

## Security & Configuration Tips
GitHub secrets: `VENICE_API_KEY` (the scheduled provider), `ANTHROPIC_API_KEY`, and optional `IMGFLIP_USERNAME`/`IMGFLIP_PASSWORD` for memes. The local provider's server address and model name (`LOCAL_AI_BASE_URL`, `LOCAL_AI_MODEL`, optional `LOCAL_AI_API_KEY`) live in `.env`, not GitHub — they name a private host, so keep them out of `appsettings.json` and out of git. Never commit `.env` artifacts — `.env`, `.env.*`, `.claude/settings.local.json`, and `appsettings.*.local.json` are gitignored, and `.env.example` is the value-free template that is safe to commit. Only secrets should come from env vars—content defaults are maintained in `appsettings.json`. Review `_config.yml` before enabling plugins to stay within the GitHub Pages allowlist.
