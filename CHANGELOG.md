# Changelog

## 2026-09-24 — no tags from code samples
- `TagInferrer` drops fenced code blocks before it looks at the post. A dotted identifier in a sample (`context.Response.StatusCode`) read as a versioned name and outranked the real topics, and a `#` comment line in a bash block counted as a heading.

## 2026-09-24 — DeepSeek 4.1 Flash as the Venice brain
- `VeniceBrainModel` is `deepseek-v4-1-flash` (from `grok-4-6`), with `grok-4-6` first in `VeniceBrainFallbackModels`. Replaying one run's research prompts: ~$0.065 and ~23 s per four-pass run against Grok 4.6's ~$0.19 and ~85 s; no note URL missing from the pass's citations, no reasoning text in the notes, and it reports "None" for an empty freshness window rather than stretching older items into it. Only two DeepSeek runs so far, hence the Grok fallback.
- Tried and not adopted: `grok-4-7` (same price as 4.6 but ~50% more spend per run and slower), `claude-opus-5-5` (fastest, ~$0.43/run), `xiaomi-mimo-v2-5` (3 of 12 passes hit the 5-minute timeout), `z-ai-glm-5-3` (thinking consumed the whole 6000-token research budget and returned empty notes; with thinking disabled it writes its reasoning into the notes).

## 2026-09-23 — drop the Sonnet 4.6 writer fallback
- `VeniceWriterFallbackModels` is just `zai-org-glm-5-2`. `claude-sonnet-4-6` was a same-provider step down from the `claude-sonnet-5` writer, costing more per token.

## 2026-09-22 — direct Anthropic path on Sonnet 5
- `AnthropicModel` is `claude-sonnet-5` (from `claude-sonnet-4-6`): stronger, and $2/$10 per MTok against $3/$15. It matches the Venice writer, so both paths now write in the same model.
- `AnthropicTemperature` is `null`. Sonnet 5 rejects a non-default `temperature` with a 400, so the old `0.9` would have failed every Anthropic run.
- `AnthropicMaxTokens` is 16000 (was 4096). Sonnet 5 runs adaptive thinking when `thinking` is omitted, and `max_tokens` caps thinking and the article together.
- `AnthropicProvider` now throws on `stop_reason` `max_tokens` or `refusal` instead of publishing whatever partial text came back. Covered by `AnthropicProviderTests`.
- Scheduled runs are unaffected — they use Venice (`DEFAULT_AI_PROVIDER`); this is the manual-dispatch `anthropic` option.

## 2026-09-18 — the generator can see what it already published
- `PublishedHistory` reads the last `RecentPostHistoryCount` posts (12 by default) out of `_posts/` — date and front-matter title — and `PromptBuilder` puts that list in front of both the research stages and the writer, with a rule to open ground the archive does not already cover.
- This was the actual cause of the repeats, not the window. The generator had no cross-run state at all: `_posts/` was touched only to avoid a filename collision, the four research angles in `PromptBuilder.ResearchAngles` are fixed strings, and Venice's search is one retrieval pass per angle — so identical queries returned the same evergreen pricing pages every run. Five of the nine posts before this were Azure OpenAI cost pieces; 09-13 and 09-15 shared four of seven sources, and 09-17 repeated the 09-13 title formula verbatim.
- The research stages get the list too, deliberately. Telling only the writer to avoid a subject while the brain keeps bringing back a dossier made of that subject yields a worse post on the same topic rather than a different one.
- The rule is "pick different ground", not "never mention these words" — a real in-window story about Azure pricing is still the post worth writing, so the instruction is to lead with what is new since the earlier post and say what changed.
- Prompts are assembled with a section that drops out entirely when there is no archive (a fresh checkout, or `RecentPostHistoryCount: 0`), and the blank run it would leave behind is collapsed.
- Not done yet: the fixed research angles, and demoting URLs the archive already cited when the Venice dossier is merged. Worth measuring what the history alone buys first.

## 2026-09-18 — narrower freshness window
- `RecentWindowDays` is back to 2 in `appsettings.json` (it had been 3). The Tue/Thu/Sun cadence has 2- and 3-day gaps, so a 3-day window let consecutive runs research overlapping days: the Sunday 09-13 and Tuesday 09-15 posts shared four of seven sources and landed on the same Azure OpenAI PTU/pricing material.

## 2026-09-11 — tags are links now, and there is a page to browse them
- Every tag chip on the home page and in search results is a link to `/tags/?tag=<tag>`, which lists just the posts carrying that tag. The chip for the tag you are filtering by is highlighted in each card, so it is obvious which of a post's tags matched.
- `/tags/` with no query renders the whole tag cloud, sorted by post count, with the count on each chip. Tags are inferred per post, so roughly half of them are used exactly once; those are collapsed behind a "show N more used once" button rather than burying the tags that actually group posts.
- The tag page reads the existing `search.json`, so there is no second index to build and no plugin: `jekyll-archives` is not on the GitHub Pages allowlist, and Jekyll cannot generate a page per tag without one. Filtering is client-side, and the tag is a query parameter so a filtered view is still a shareable URL.
- Busy tags render in batches of 24 with a "show more" button — `#.net` matches 240 posts, and dumping 240 cards into the DOM on load is a lot of layout for a page most visitors will use to find one post.
- Card rendering moved out of `search.html` into `assets/js/post-cards.js`, shared by search and tags, so the two pages cannot drift on how a card or a tag link is built.
- Fixed a header bug this surfaced: minima makes `body` a flex column while `styles.css` pins `body` to `height: 100%`, so flex items shrink below their content. The header search row was overflowing the header and landing on top of the first element on the page — visible on the new tag page, where it covered the `#tag` heading at phone width. `.site-header` is now `flex: 0 0 auto`.
- A "Browse tags" link sits in the footer, since otherwise the tag cloud is only reachable by clicking a tag first.

## 2026-09-11 — the local model does its own research, from feeds
- The `local` provider now researches before it writes, instead of depending on a caller to hand it a dossier. `dotnet run --project BlogGenerator -- local` with no flags produces a grounded post.
- This became possible because tool calling now works on every chat model on `main` — measured across all six, both emitting a call and using the result handed back. The `--jinja` chat templates plus llama.cpp's autoparser derive the call format from the template rather than a hardcoded model list.
- It is still one model. Research and writing are two passes of the *same* model in the same run, so llama-swap never evicts anything mid-run: the earlier "a second stage means a second model" concern does not apply to two passes of one.
- The research surface is publisher feeds and nothing else — `Generation:ResearchFeeds`, ten of them, with no search API and no key. Feeds fit the question this blog actually asks: "what shipped in the last three days" wants a dated list from publishers we already trust, which is what a feed is, where a search API would cost money and return an undated relevance ranking.
- Two tools: `list_recent_posts` (filterable by source or keyword) and `fetch_page`. The model is told plainly that a feed summary shows a thing exists but not what changed, so it should fetch anything it intends to report.
- The in-window/older split is computed in `FeedResearchTools`, not asked of the model. A stage whose whole job is a freshness window should not depend on a small local model subtracting dates correctly.
- Fetching is limited to URLs a feed actually returned, the feeds' own hosts, and `AllowedDomains`, minus `BlockedDomains`. Tracking the URLs the feeds handed back is what makes this exact rather than a guess: a publisher's feed and its articles routinely live on different hosts (`feed.infoq.com` serves links to `www.infoq.com`), and inferring a parent domain from a host would be both wrong and over-permissive.
- A tool that cannot answer returns an explanation for the model to read rather than throwing. An unreachable feed, a refused host, a malformed argument blob, and a hallucinated URL are all normal mid-research events, and the model can route around each of them; only the whole stage producing nothing is an error.
- The skill takes arguments now: `ungrounded` / `evergreen` forces `--no-research`, `feeds` forces the model's own research even when the agent could search, and anything else is a topic steer. Wanting an evergreen piece on a slow news day is a request, not a failure mode, so it stopped being only a fallback.
- `--dossier <path>` still wins when supplied — an agent with real web search does better research than ten feeds — and `--no-research` skips the stage for an evergreen post. The skill now tells the agent to research itself when it can, and to read the run log's tool lines to see how many sources the model really opened.
- The feed client decompresses automatically. Tech Community returns a gzip body whether or not it was negotiated, and without that the feed arrived as binary and failed to parse — which the research pass honestly but unhelpfully reported as a malformed feed. All ten shipped feeds now parse.
- `HtmlText` collapses source whitespace before it inserts line breaks at block boundaries. Newlines in HTML are insignificant whitespace, and a publisher who hard-wraps their markup was otherwise getting line breaks mid-sentence in the text handed to the model.
- `LocalResearchMaxTokens`, `LocalResearchTemperature`, `LocalResearchMaxRounds`, `ResearchFeeds`, `ResearchFeedItemsPerSource`, and `ResearchPageMaxChars` join `appsettings.json`. Research temperature is 0.3, matching the Venice brain stage for the same reason.

## 2026-09-10 — stop tagging fragments of comma-grouped numbers
- `000-character` shipped as a tag on a published post. The prose said "a 4,000-character chunk"; the token pattern has to exclude commas, so the number split into `4` (too short to tag) and `000-character` (long enough, carries a lowercase letter, not a stopword). The fragment then satisfied `CollectSalientTokens`' "carries a digit or a hyphen" test, was ranked as a versioned identifier, and outranked the post's real topics.
- `NormalizeTag` now rejects any token whose leading component is nothing but digits, which covers the whole class: `000-character`, `500-ms`, `30-80`. Because `CountTags` and `CollectSalientTokens` both normalize, one check fixes the ranking and the tag list together.
- The cost is the occasional real term like `64-bit`, which now normalizes away too. That is a fair trade — a quantity has never made a tag worth filtering the blog by, and measurement fragments are common in posts that quote latencies or sizes.
- Names that merely *contain* digits are untouched: `gpt-5.4`, `claude-sonnet-5`, `qwen38-27b`, `.net`, and `asp.net` all still tag, since only a leading digit run marks a measurement.
- Published posts keep the tags already in their front matter; this affects future generation only.

## 2026-09-10 — generate posts on the local GPU box
- Added a `local` provider (`dotnet run --project BlogGenerator -- local`) that talks to a self-hosted OpenAI-compatible server. `LOCAL_AI_BASE_URL` and `LOCAL_AI_MODEL` come from the gitignored `.env` — they name a private host, not a content default — with an optional `LOCAL_AI_API_KEY` for a server that is not open on the LAN. The base URL accepts a bare host (`main:8080`), a root URL, or one already ending in `/v1`.
- Deliberately single-model and single-call. Venice's brain/writer split assumes both models sit behind an API; a local box has one GPU pool. On `main` that is literal — llama-swap keeps one model resident on the two 16 GiB cards and evicts it to load another — so a second model would mean a swap mid-run.
- The provider has no web search, so it never asks the model to search. It composes from a research dossier passed with `--dossier`, reusing `PromptBuilder.WriterSystemPrompt`/`WriterUserPrompt`, which already forbid printing a URL that is not in the dossier. Run without `--dossier` and a stand-in dossier saying no search happened is sent instead, which routes the writer to evergreen mode with no links rather than links from memory.
- Added `.claude/skills/local-post/SKILL.md`, a project-scoped skill that is the intended caller: the agent researches with its own web tools, writes the dossier, runs the generator, and then reviews the post — checking first that every URL in it came from the dossier, which is the likeliest way a local model goes wrong.
- Posts from this provider are tagged `local` alongside the model name. `AIProviderResponse` gained `ExtraTags` for provenance tags that are not model names, and `TagInferrer.Infer` keeps them when trimming exactly as it keeps model tags, spending a topic slot rather than dropping one off the end — `tags: [kernel, .net, integration, response, local, gemma-26b]`.
- Model tags are now sanitized for YAML. Front matter writes tags as a flow sequence, and local model ids routinely carry a colon or slash (`qwen3:32b`, `hf.co/unsloth/...`), which would have turned `tags: [...]` into a mapping. Those separators become hyphens; hosted ids like `claude-sonnet-5` are unchanged.
- `LocalMaxTokens`, `LocalTemperature`, `LocalTopP`, and `LocalTimeoutMinutes` join `appsettings.json`. The timeout defaults to 30 minutes, generous on purpose: a cold llama-swap pages new weights onto the GPUs before the first token, and a timeout there reports that rather than looking like a dead server. Hitting the token cap logs a warning that the post may be cut off.
- The `<think>`-block strip moved out of `VeniceProvider` into a shared `ModelText` helper, since a local llama.cpp build hands reasoning back inline where hosted APIs strip it.
- Smoke-tested end to end against `gemma-26b` on `main`: a post written from a dossier, tagged `local, gemma-26b`, whose only external link was the one URL the dossier supplied. The test post was not published.

## 2026-09-08 — real favicons instead of the 800x533 logo
- `_includes/header.html` declared `rel="icon"` as `type="image/svg+xml"` while pointing at `assets/images/robot.webp` — an 800x533, 140 KB illustration with EXIF and an ICC profile. Every visitor downloaded it for a 16px tab icon, and Firefox stored the decoded result in its favicon cache as a 1 MB PNG.
- Added a proper icon set cropped square to the robot's head so it still reads at 16px: `favicon.ico` at the site root (16/32/48, 15 KB), `assets/images/favicon-16.png` (1 KB), `favicon-32.png` (2.5 KB), and `apple-touch-icon.png` (180x180, 17 KB, quantized to 128 colours). The header now declares each with the right `type` and `sizes`.
- The header logo `<img>` was the same full-size file scaled down by CSS to 56px tall. It now uses `assets/images/robot-logo.webp` (168x112, 5.4 KB — 2x the rendered height) and carries `width`/`height` so it does not shift layout on load.
- `assets/images/robot.webp` stays in place: seven published posts embed it inline as their meme image.

## 2026-08-28 — stop tagging sentence punctuation
- Tags no longer keep the punctuation that ended the sentence they came from. `everywhere.`, `client.`, `vs.`, and `a.` had all shipped as tags, because the token pattern has to allow a dot for `.net`, `asp.net`, and `gpt-5.4` and could not tell those apart.
- The dot did more than look wrong: `CollectSalientTokens` treats any token carrying a digit, dot, hyphen, or plus as a versioned identifier, so an ordinary word with a full stop attached was ranked *above* the real topics. Salience is now judged on the trimmed word.
- Only a trailing run of `.`/`-` is dropped, so a leading dot (`.net`) and a trailing plus (`c++`) survive. Trimming happens before the stopword and length checks, so `not.` is now caught as a stopword and `vs.` as too short.
- Added `actually` and `already` to the generic-English stopwords. They were previously crowded out only because punctuated junk outranked them; with that junk gone they need naming outright.
- Re-inferring all 298 published posts changes 42 tag lists, almost all reordering; the only tags lost are `client.`, `request.`, `keep`, `billing`, and `aigateway`, and the ones gained are `endpoint`, `management`, `request`, and `stack`. Published posts keep the tags already in their front matter — this affects future generation only.

## 2026-08-28 — tag every model that wrote the post
- Posts now carry a tag for each model that contributed, not just the one that produced the prose. Under Venice's brain/writer split the front matter said `claude-sonnet-5` alone, crediting the writer and leaving the `grok-4-6` research passes invisible; a two-stage run is now tagged `grok-4-6, claude-sonnet-5`.
- `AIProviderResponse.UsedModel` became `UsedModels` (a list). Anthropic and Foundry are unchanged at the call site — a single-model constructor overload wraps their one model — and Venice concatenates the research models it actually reached with the writer's, deduplicated.
- Only a research pass that returned usable notes credits its model, so a brain model that came back empty is not tagged on a post it did not ground.
- `TagInferrer.Infer` takes the model list and keeps every model tag when trimming, spending topic slots instead: one model still leaves room for five topics as before, two leave four, and the total stays at six.

## 2026-08-28 — code samples
- Rewrote the prompt's code guidance. The old rule was one clause — "Clear sections with practical takeaways (code or CLI snippets welcome)" — with no bar on length or accuracy, and the output showed it: across the 2026 posts a third of code blocks were four lines or fewer, 7% were nothing but `dotnet add package` lines, and 7 of the last 30 posts' 13 C# blocks opened with `// Pseudocode:`, `// Conceptual sketch:`, or `// Pseudocode-ish shape`. Several were built on APIs that don't exist (`GitHub.Copilot.SDK`, `new AzureOpenAI(apiKey:, endpoint:)`).
- `PromptBuilder.CodeGuidance` now asks for at most one code block per post, sized by `CodeSampleMinLines`/`CodeSampleMaxLines` (15-30), showing a complete unit of work — imports, real type names, and the config or error path around the call. It tells the model to omit the block entirely when it cannot write against a verified API surface, on the grounds that no code beats invented code, and bans the pseudocode/conceptual/illustrative labels, `NotImplementedException`, `...` placeholders, and blocks made only of package-install or CLI-invocation lines. Because the guidance lives in the shared `GuidanceBlock`, all three providers and the Venice writer stage inherit it.
- Added `CodeSamplesEnabled`, `CodeSampleMinLines`, and `CodeSampleMaxLines` to `appsettings.json` and `GenerationSettings` (validated only when samples are enabled). Disabling the flag swaps in a "no code blocks" instruction rather than leaving the model to guess.
- Raised `PostWordsMax` from 1000 to 1200. Code counts toward the word budget, so a 15-30 line sample would otherwise be paid for out of the prose.
- Smoke-tested end to end against Venice (the scheduled provider). The generated sample was 35 lines of real `AzureOpenAIClient`/`ChatClient` code that handled a 429 on a Provisioned Throughput deployment by falling back to a pay-as-you-go one — a real API surface, a real failure path, and no pseudocode label. The linter's only complaint was the five-line overshoot. The test post was not published.
- Added `CodeSampleLinter`, which re-checks the finished post and prints warnings to the run log. It is deliberately advisory and never edits the post: the pipeline publishes unattended, so deleting a block would leave the sentence introducing it pointing at nothing. It checks the one-block rule, the disclaimer wording, stubs and `...` placeholders, install-only blocks, and both ends of the size band. Run over the existing 298 posts it flags 207 of them, which is the baseline to watch improve.


## 2026-08-28 — generator refactor
- Consolidated provider plumbing into `BlogGenerator.Core/Providers/ProviderSupport.cs`. Resolving a secret from the environment, redacting that secret out of error messages, ordering the primary-then-fallbacks model list, and posting a JSON body with the API's own error text on failure were each written two or three times across the Anthropic, Foundry, and Venice providers; they are now written once.
- Collapsed Venice's two generation paths — the single search-and-write call and the brain/writer pair — onto one `WriteAsync` helper, and removed `PromptBuilder.BuildChatMessages`, which built the same request in a different message shape than the rest of the provider.
- Extracted the length/output-format rule and the "never break character" rule that the single-call and Venice writer system prompts both carry. Prompt text is unchanged word for word across all 48 generated variants; only the hard-wrapping of one paragraph moved.
- Trimmed `PromptContext` to the five members providers actually read. `ModeInstructions`, `PrimaryLinkLine`, and `UserInstructionItems` were assembled on every run but only ever read back by tests, which now assert against the prompt that actually ships.
- Removed dead code: `PromptBuilder.EmptyResponseRetryInstruction` and `MarkupRetryInstruction` (no callers), `MemeExtractor.ExtractTldrLine` (no callers — the house style prompt bans TL;DR labels outright), `PostWriter.HeadingRegex`, and the never-read `url`/`box_count`/`page_url` fields on the imgflip DTOs.
- Dropped the unused `YamlDotNet` and `Microsoft.Extensions.Options` package references, and replaced the generic host in `Program.cs` — built but never started, and its configuration unused because settings load from a separate `ConfigurationBuilder` — with a plain `ServiceCollection`.
- Deduplicated `GenerationSettings.Normalize` into one list-cleaning helper and folded thirteen hand-written validation throws into a single `Require` helper.
- Hoisted the Foundry `ResponsesClient` construction out of the model-retry loop, and inlined the `BuildWebSearchTool` wrapper that discarded its only argument.

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
- Stopped writing posts with a UTF-8 byte-order mark in front of the YAML front matter; `PostWriter` now uses a BOM-free UTF-8 encoding. The 137 pre-existing posts that carried one (2026-03-14 through 2026-08-27) were stripped in a follow-up pass, so the archive is now uniformly BOM-free; Jekyll reads sources as `bom|utf-8` and stripped the marker itself, so nothing rendered differently before or after.
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
