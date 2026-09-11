---
name: local-post
description: Generate a blog post with the self-hosted model on `main` instead of a paid API. Use when asked for a local post, an offline/self-hosted post, a post "on main", or a free post that does not spend Anthropic/Venice credits.
---

# Local post

Writes one post with a model running on the LAN box (`main`), for free, then commits and pushes
just that post.

The model researches for itself. It has no search provider behind it, so instead it reads the
publisher feeds in `Generation:ResearchFeeds` and fetches the articles it wants, then writes the
post from the dossier it produced. You do not have to supply anything for that to work.

**If you have web search, use it anyway.** Your own research beats ten feeds — you can follow a
story across sources the feed list does not carry. Gather it, write a dossier, and pass it with
`--dossier`; that overrides the feed pass entirely. Reach for the feeds when web search is
unavailable to you, or when the run is meant to be hands-off.

## Options

Read what the user typed after the skill name. No argument is the normal case.

| Argument | Effect |
|---|---|
| *(none)* | Research if you can, feeds if you cannot. The default below. |
| `ungrounded`, `evergreen`, `no news` | Force `--no-research`: a timeless piece with no sources. Skip step 3 and most of step 5. |
| `feeds` | Skip your own research and let the model use its tools, even if web search is available to you. |
| anything else | A topic steer. It only bites when you do the research yourself in step 3 — the feed pass takes no topic — so if you end up on feeds, say in your report that you could not honour it. |

`ungrounded` is a legitimate request, not a failure mode — a slow news day is a fine reason to want
an evergreen piece. Hold it to a higher prose bar, though: with no sources there is nothing but the
writing to carry it, so if it reads thin, regenerate rather than ship.

## Why one model

`main` runs [llama-swap](http://main:8080/ui), which keeps **one** model resident on the two
16 GiB cards and evicts it to load another. Venice's brain/writer split is not available here.
But single-model is not single-pass: research and writing are two passes of the *same* model in
the same run, so nothing is swapped or loaded twice.

## Steps

1. **Check the server.** `curl -s http://main:8080/v1/models | head -c 400`.
   - No answer: `main` is probably asleep. Wake it from dad with `~/bin/wake-main.sh` (it comes
     back in ~6 s), then retry. If it still does not answer, stop and say so — do not silently
     fall back to a paid provider.
   - The `status` field on each model tells you which one is already loaded. Loading a different
     one costs a swap of a minute or so, which is fine but worth expecting.
2. **Confirm the config.** `LOCAL_AI_BASE_URL` and `LOCAL_AI_MODEL` live in the repo's gitignored
   `.env` (template in `.env.example`). Read `.env` for the model id; if it names a model the
   server does not list, say so and ask rather than guessing a replacement.
3. **Research, if you can.** Skip this step entirely for `ungrounded` or `feeds`.
   Otherwise use your own web search/fetch to find what shipped in the last
   `RecentWindowDays` (see `appsettings.json`) that matters to engineers on .NET, Azure, or AI.
   Make at least four genuinely different searches — vendor engineering blogs, official release
   notes and changelogs, GitHub releases, developer press — not four rewordings of one query.
   Prefer the `AllowedDomains` list. Fetch the pages you cite; do not cite a URL you only saw in
   a search snippet. Write the dossier to the scratchpad (not into the repo) in exactly this
   shape — it is what `PromptBuilder.WriterSystemPrompt` expects:

   ```markdown
   ## In-window findings
   Items whose PRIMARY announcement date is inside the window. What shipped, who shipped it,
   the exact date, why an engineer should care. Write "None." if there are none — never stretch
   an older item into the window.

   ## Context
   Older or undated background worth having, each marked with its real date.

   ## Sources
   Every URL you actually used, one per line, plain URL followed by a short title.
   ```

   Every URL the post is allowed to print has to appear here verbatim, so the Sources section is
   the post's whole link budget. Copy dates and version numbers exactly as the source states them.

   Skip this step if web search is unavailable — the model's own feed pass covers it.
4. **Generate:**
   ```bash
   dotnet run --project BlogGenerator -- local                     # model researches the feeds
   dotnet run --project BlogGenerator -- local --dossier <path>    # your research instead
   ```
   Expect this to be slow — the research pass plus several minutes of local decode, plus a model
   swap if one is needed. The run prints the post path, and when the model researched for itself
   it also prints where it saved its dossier. Keep that path; step 6 needs it.

   ```bash
   dotnet run --project BlogGenerator -- local --no-research      # evergreen, no sources
   ```
   Use the third form when the user asked for `ungrounded`, or when the feeds are unreachable.
   Either way, say plainly in your report that the post has no sources.
5. **Read the run log** before the post. It tells you what actually happened:
   - `round N: <tool>(...)` lines — how many sources the model really looked at. One
     `list_recent_posts` and no `fetch_page` means it wrote from feed summaries alone, which is
     thin; say so.
   - `tool budget of N round(s) spent` means it ran out of rounds rather than finishing, so the
     dossier may be half-formed.
   - `Could not read: <source>` means a publisher was down or rate-limiting, and that source was
     not searched at all.
6. **Review the generated post.** Local models miss house rules that hosted ones follow:
   - Every URL in the post appears in the dossier. **Any link that is not in the dossier is
     invented — delete it.** This is the single most likely failure, and it is why the dossier
     path is worth keeping: diff the two.
   - Dates and versions match the sources, and news framing ("this week", "just landed") is only
     used if the lead really is in the window. The tools already sorted items into in-window and
     older, so a mislabelled lead means the model overrode what it was told.
   - Front matter is sane: real title, `tags` ends with `local, <model>`.
   - No leaked reasoning, no "as an AI", no meta commentary about the dossier or the instructions.
   - Any code block follows `PromptBuilder.CodeGuidance`; watch the run log for
     `Code sample:` warnings.
   - Read the prose. A 26B model writes competently but not always well — if it is not worth
     publishing, say so plainly rather than shipping it.
7. **Report** the path, the model used, what the research pass found, and anything you fixed.
8. **Commit and push the post** to `master`. Stage the post file *and nothing else* — a local run
   often sits on top of unrelated working-tree changes, and this commit should be the post alone:

   ```bash
   git add <post path>
   git commit -m "chore(posts): daily AI article (local, <model>)"
   git push origin master
   ```

   `git status --short` first and confirm nothing else got staged. The post is live once GitHub
   Pages rebuilds, so this step is the publish — if step 6 found problems you could not fix, or
   the prose is not worth publishing, **stop and say so instead of pushing**. Fix what you can in
   the post file before committing; a rerun is free, so prefer regenerating over heavy editing.

## Notes

- Nothing here costs money or leaves the LAN, so a rerun is cheap. If the first post is weak, try
  a different model from `/v1/models` (`LOCAL_AI_MODEL` in `.env`) rather than editing it heavily.
- If the run reports the token cap was hit, the post is cut off mid-sentence: raise
  `Generation:LocalMaxTokens` in `appsettings.json` and rerun. Same for a timeout and
  `Generation:LocalTimeoutMinutes`, and for a research pass that keeps running out of rounds and
  `Generation:LocalResearchMaxRounds`.
- A feed that is consistently unreadable belongs in `Generation:ResearchFeeds` as a fix, not as a
  note in a post. Do not add a keyed search API — this provider exists to cost nothing.
- Do not commit `.env`, and do not move the server address or model name into `appsettings.json`.
