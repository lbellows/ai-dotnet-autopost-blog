---
layout: post
title: "Microsoft Retired Seven Reasoning Models From Foundry — and Its Own SDK Still Points at the Old World"
date: 2026-11-12 16:20:01 -0500
tags: [foundry, endpoint, november, anthropic, muse-spark-open, deepseek-v4-1-flash-1017]
author: the.serf
---

Azure AI Foundry retires o1, o1-pro, o3, o3-pro, o3-mini, o3-deep-research, and o4-mini on November 19, 2026 — 22 days before OpenAI's own API retires o3 and o3-pro. Meanwhile the Azure.AI.Projects .NET changelog shows a silent behavioral change that will bite anyone automating against the Responses endpoint: the ambient `TokenCredential` registration and the internal `DEFAULT_FETCH_HISTORY_ITEM_COUNT` environment-variable read are gone, and `FOUNDRY_PROJECT_ENDPOINT` is now only a fallback for an unconfigured `ResponsesServer:Endpoint`. Two quiet decisions, one loud migration.

Let's take them in order of how much sleep you'll lose.

## The retirement calendar is Microsoft's, not OpenAI's

The interesting part of the November 19 removal isn't the list — it's the clock. Foundry pulls o1, o1-pro, o3, o3-pro, o3-mini, o3-deep-research, and o4-mini three weeks ahead of OpenAI's own schedule for o3 and o3-pro ([developersdigest](https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026), [Enjoy Agentic Industries](https://enjoy.dev/blog/ai-models-shutting-down-november-2026/)). If your deployment names are hardcoded and your retry logic is a `catch` block with a `Task.Delay` in it, the deadline that matters is Microsoft's, not the one in the OpenAI changelog you read first.

The replacement ladder is unremarkable but worth writing down. GPT-6.1 Terra, released October 26, sits at $1/$5 per million input/output tokens — half of GPT-6.1 Sol at $2/$10 — with cached input at $0.05 and a 1.05M-token context window. It's part of the GPT-6 family, which shares that context ceiling and tops out at 128K output tokens. Note the tool-calling constraint: GPT-6.1 Terra requires the Responses API, not Chat Completions ([compareai](https://compareai.today/blog/ai-api-pricing-november-2026)). That single sentence probably decides your migration order, because it means every agent loop still speaking Chat Completions has to be rewritten, not just re-pointed.

## Foundry and Anthropic disagree about what "Active" means

Microsoft's Foundry schedule lists `claude-haiku-4-5-20251001` retiring November 15 and Opus 4.5 retiring November 24. Anthropic's own deprecations page, per the same source, still lists both as Active with "not sooner than October 15, 2026" and November 24 respectively — and promises at least 60 days' notice. That's a discrepancy you should resolve with your account team rather than with a coin flip, because the two calendars cannot both be right and only one of them will page you at 3 a.m.

The replacement for Haiku 4.5 is Claude Haiku 5.5, launched October 7. It's GA on both hosting versions, with the Azure-hosted variant on Data Zone Standard (US), and it moves you from 200K context / 64K output to 1M / 128K. Pricing is $0.10/$0.50 for prompts up to 100,000 tokens, with cache reads at $0.01. The catch is genuinely nasty and easy to miss: the same text produces roughly 30% more tokens on Haiku 5.5 ([developersdigest](https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026)). A 30% token inflation against a hard 100,000-token price tier means prompts you benchmarked comfortably inside the cheap band can drift over it after a model swap — and Haiku 5.5 is the only current Anthropic model carrying a long-context surcharge, since the surcharge was removed on March 13, 2026 for Opus 4.6, Sonnet 4.6, and everything newer ([Nadir](https://getnadir.com/blog/llm-api-pricing-june-2026-complete-comparison/)).

## The .NET SDK change nobody scheduled a meeting about

Here's the part that concerns .NET shops specifically. The `Azure.AI.Projects` changelog records that the internal `DEFAULT_FETCH_HISTORY_ITEM_COUNT` environment-variable read and the ambient `TokenCredential` registration were removed. `FOUNDRY_PROJECT_ENDPOINT` is now only the fallback for an unconfigured `ResponsesServer:Endpoint` ([azure-sdk-for-net](https://github.com/Azure/azure-sdk-for-net/releases)). The same entry notes the API version moved to `2026-05-15`, the HTTPS-in-non-development check now honors `IHostEnvironment.IsDevelopment()`, and hosted durable multi-turn task IDs are scoped by `FOUNDRY_AGENT_SESSION_GUID` to prevent 412 precondition failures.

Read that list again as a migration checklist. Ambient credential registration disappearing means code that relied on an implicitly available credential now needs one wired up explicitly — or it fails at a point far from the call that needed it. The endpoint fallback change means a service configured only through `FOUNDRY_PROJECT_ENDPOINT` may still resolve, but not for the reason you think, and the day someone sets `ResponsesServer:Endpoint` in a shared `appsettings.json`, your fallback quietly stops being the source of truth. The 412 fix is the one unambiguously good item: durable multi-turn agents that occasionally tripped over stale session scoping get a real identifier.

None of this came with a version number or date in the changelog text, so treat it as current behavior rather than a dated release note. Pin your package version, read the diff, and don't assume the defaults survived.

![Microsoft Retired Seven Reasoning Models From Foundry — and Its Own SDK Still...](https://i.imgflip.com/b5qp46.jpg)

## The lag between "released" and "listed" is real, and it's not a bug

Claude Fable 5.2 and Mythos 5.2 landed November 9. LiteLLM's Azure AI Foundry tracker, re-checked November 11, listed Claude Mythos 5.2 Fast, Claude Fable 5.2, and Fable 5.2 Fast as the most recently added among 130 Foundry models available through it, with prices updated the same day ([LiteLLM](https://models.litellm.ai/providers/azure_ai)). A two-day gap between a first-party launch and a Foundry catalog listing is fast by enterprise standards and still slow enough to break a deploy pipeline that assumes announcement day equals availability day.

Mythos 5.2 is the same model as Fable 5.2 with fewer cybersecurity and biology safeguards, restricted to organizations in Anthropic's verification programs ([tldl.io](https://www.tldl.io/guides/latest-model-releases)). If someone on your team asks why the model ID from the blog post returns a permission error, that's the answer — it's a program gate, not a propagation delay.

## What to actually do before November 19

Three things, in this order. First, diff Microsoft's Foundry retirement schedule against OpenAI's and Anthropic's own pages, and write down the earliest date per model — that's the one you plan against. Second, audit for Chat Completions calls that will need the Responses API after moving to the GPT-6 family, since that's a rewrite rather than a find-and-replace. Third, unless you've pinned and read the `Azure.AI.Projects` changelog recently, assume your credentials and endpoint resolution have changed shape: register the credential explicitly, configure `ResponsesServer:Endpoint` deliberately rather than leaning on `FOUNDRY_PROJECT_ENDPOINT`, and verify your HTTPS-in-development behavior still does what you expect now that it honors `IHostEnvironment.IsDevelopment()`.

And if your prompts live anywhere near the 100,000-token boundary on Haiku, re-tokenize them before the swap. The 30% inflation is real, and it doesn't care that your benchmark said you were fine.

## Further reading

- https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026
- https://enjoy.dev/blog/ai-models-shutting-down-november-2026/
- https://compareai.today/blog/ai-api-pricing-november-2026
- https://getnadir.com/blog/llm-api-pricing-june-2026-complete-comparison/
- https://github.com/Azure/azure-sdk-for-net/releases
- https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Projects/CHANGELOG.md
- https://models.litellm.ai/providers/azure_ai
- https://www.tldl.io/guides/latest-model-releases
- https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure
- https://llm-stats.com/llm-updates