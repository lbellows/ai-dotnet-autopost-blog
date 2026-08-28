# Changelog

## 2026-08-28
- Resolved the daily workflow's AI provider in one place instead of repeating it across steps. A `Resolve provider` step now picks the `workflow_dispatch` menu choice when there is one and the `DEFAULT_AI_PROVIDER` workflow env value otherwise, and later steps consume its output. Switching the scheduled provider is now a one-line edit.
- Fixed the manual and scheduled runs disagreeing on a default: the dispatch menu previously defaulted to `anthropic` while the schedule fell back to `foundry`. The menu now offers `scheduled-default`, which defers to the same configured value the schedule uses.
- Pointed the scheduled run at `venice`.
- Added a Venice.ai provider (`dotnet run --project BlogGenerator -- venice`) targeting Venice's OpenAI-compatible chat-completions endpoint with grounding via provider-side web search (`venice_parameters.enable_web_search`).
- Split Venice generation into a brain/writer pair because Venice's web search is a single retrieval pass per request rather than an agentic tool loop: `grok-4-6` runs one grounded research pass per source angle (vendor blogs, changelogs, GitHub releases, news coverage) and `claude-sonnet-5` writes the post from the merged dossier with search off. Clearing `VeniceWriterModel` collapses this to a single search-and-write call.
- Both Venice stages fall back through an ordered model list, and a failed research pass is logged and skipped so the remaining passes still ground the post.
- Stripped Venice's inline superscript citation markers (`^4^`, `^1,5,8^`) and any surviving `<think>` block from model output before posts are written.
- Extracted the shared post-style checklist into `PromptBuilder.GuidanceBlock` (now carried on `PromptContext`) so the Venice writer stage and the single-call providers stay in sync on house style.
- Taught the generator to load a gitignored `.env` at startup, with real environment variables taking priority, and added `.env.example`. Broadened `.gitignore` to cover `.env.*`, `.claude/settings.local.json`, and `appsettings.*.local.json`.
- Added `venice` to the daily workflow's provider choice, wired `VENICE_API_KEY` through both steps, and added its missing-secret guard.
- Stopped writing posts with a UTF-8 byte-order mark in front of the YAML front matter; `PostWriter` now uses a BOM-free UTF-8 encoding. Existing posts still carry their original BOM.
- Reworked tag inference, which was emitting filler like `between`, `bring`, `actually`, and `already` alongside source domains like `github.blog`. Candidates are now ranked by whether they look like names — capitalized mid-sentence in body prose, or carrying a digit/dot/hyphen — before whole-body frequency, instead of by alphabetical tiebreak over heading tokens alone. Salient body terms also compete as candidates so conversational headings no longer starve the list, URLs are excluded from that signal, and domain-shaped tokens are rejected outright (with `.net` and `asp.net` explicitly spared). The generic-English stopword list was expanded to match.
- Added tests for Venice model-candidate ordering, citation-marker and thinking-block cleanup, response/citation parsing, dossier assembly, and the research/writer prompts, plus a test asserting the shipped `appsettings.json` still satisfies `GenerationSettings.Validate()`.

## 2026-06-14
- Reduced the publishing cadence from daily to Tue/Thu deep-dive posts plus the Sunday week-in-review synopsis by changing the workflow cron to `15 11 * * 2,4,0` (the generator already switches to synopsis mode automatically on Sundays).
- Widened `RecentWindowDays` from 2 to 5 in `appsettings.json` so the Tuesday run still covers news from the preceding Friday–Monday gap under the new schedule.
- Noted the client-side `search.json` growth limit as a future task (see README Notes); no technical archiving needed — repo/build are well within GitHub Pages limits.
- Fixed meme template mode-collapse: the imgflip template catalog is now presented to the model in a per-run shuffled order with an explicit "vary your choice" instruction, so it stops defaulting to "Two Buttons". Consolidated the duplicated name/box-count lists into a single `PromptBuilder.ImgflipTemplateCatalog` source of truth (removing the unused `ImgflipTemplateList` const) and added tests covering the shuffle.
- Corrected stale docs: `_posts/` is tracked in git and committed by the workflow (AGENTS.md previously said "left untracked"), and memes are rendered via the imgflip API from a model-chosen template (README previously implied they came from `assets/images/robot.webp`, which is only the site logo).

## 2026-03-13
- Flattened the .NET generator layout to the repo root (`BlogGenerator.sln`, `BlogGenerator/`, `BlogGenerator.Core/`, `BlogGenerator.Tests/`) and updated build, test, workflow, and docs paths to match.
- Removed the obsolete Python generator code, shared Python utilities, `requirements.txt`, caches, and virtualenv artifacts now that the C# rewrite is the only supported pipeline.
- Removed code-level fallback content/model defaults so `BlogGenerator/appsettings.json` is now the single non-secret settings source, with startup validation for missing values.
- Refactored the Azure Foundry path to use the Azure OpenAI-compatible `ResponsesClient` with `FOUNDRY_OPENAI_ENDPOINT` and `FOUNDRY_PROJECT_API_KEY`, removing the `Azure.AI.Projects`/AAD runtime dependency.
- Updated the Foundry web-search path to honor `FoundryDefaultModel` as the first deployment tried, force the preview web-search tool, and bias prompts toward `AllowedDomains`.
- Hardened Foundry startup validation so empty Azure OpenAI endpoints or API keys fail with a clear error, and updated the GitHub Actions workflow/docs to use the new Foundry secret names.
- Removed `DeepSeek-V3.2` from the default Foundry deployment list because Azure documents it as lacking tool-calling support.
- Restored the C# Anthropic generator default to `claude-sonnet-4-6`, surfaced Anthropic error bodies when a request is rejected, and deduped bound domain/model lists before building provider requests.
- Updated the daily publishing workflow to `actions/checkout@v5` so it runs on Node 24 and avoids the GitHub Actions Node 20 deprecation warning.
- Updated post generation to strip the leading markdown H1 from saved posts so the page layout title is not duplicated in rendered articles.
- Updated prompts and post writing to remove model-generated inline post metadata like `**Published:** ... ~850 words` from future posts.

## 2025-11-14
- Consolidated Claude/Foundry defaults (models, token/temperature caps, meme guidance toggle) inside `scripts/common/settings.py` so only secrets and endpoints rely on environment variables.
- Cleaned up the Azure Foundry generator to drop the unused `FOUNDARY_URL` fallback, use the shared settings object, and relocate retry prompts into `scripts/common/prompts.py`.
- Updated `README.md` to describe the new configuration knobs and cleared the completed TODO list.
- Tied `write_post` defaults (author attribution, meme generation) directly to `GenerationSettings`, so the generators no longer pass those knobs around and meme rendering follows the same setting that controls prompt guidance.
- Added Claude `max_tokens`/`temperature` and `POST_AUTHOR` defaults to `GenerationSettings` plus documentation tweaks covering the new toggles.

## 2025-10-23
- Refined `generate_post_websearch.py` to enforce a 2-day breaking-news window, weekday vs. Sunday cadence, humor, and meme prompts.
- Added optional `TOPIC_URL` workflow input plus documentation updates covering the new tunables and tone guidelines.
- Added contextual meme generation with Pillow so each post includes a fresh image saved under `assets/images/memes/`.
- Use more breaking news: time box search to only include up to last 2 days (`RECENT_WINDOW_DAYS`).
- Mix up content cadence: focused weekday posts, Sunday synopsis mode.
- Relax technology coverage: require at least one of .NET/Azure/GitHub per post.
- Add a light humorous tone.
- Prompt for meme-friendly images in the generated markdown.
- Allow workflow input to drive a specific topic or link (`TOPIC_URL`).
- Refactored generators to consume shared prompt, cadence, and meme utilities in `scripts/common/` for both Anthropic and Azure workflows.
- Automatically load local `.env` files when running generators to simplify secret management.
- Strip leading LLM instruction blocks before writing posts so published articles start at the H1 title.
- Expanded default `ALLOWED_DOMAINS` to include Microsoft ecosystem sources and high-signal tech press for fresher breaking news (replacing blocked domains like `theverge.com` and `zdnet.com` with crawler-friendly alternatives).
- Simplified GitHub Actions workflow to lean entirely on code defaults instead of duplicating tunable env vars.
- Locked generator tunables to code-level constants so only secrets come from environment variables.
- Tags are now inferred from post headings/TL;DR and include the source model name (defaulting to Claude); existing posts were retagged accordingly.
