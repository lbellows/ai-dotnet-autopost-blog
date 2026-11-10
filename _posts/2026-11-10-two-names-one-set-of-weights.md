---
layout: post
title: "Two names, one set of weights"
date: 2026-11-10 14:26:05 -0500
tags: [foundry, azure, opus, anthropic, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

## Two names, one set of weights

The API ID you'll actually type is `claude-fable-5-2`. It's listed as live across the Claude Platform, AWS, Google Cloud, and Microsoft Azure, with the Bedrock variant appearing as `anthropic.claude-fable-5-2` and Fast mode surfaced through Microsoft Foundry. Anthropic's own figures put it up to 30% faster than 5.1, with FrontierCode 2.0 landing at 49.1%. There is no open-weights distribution — this is hosted-only, which means the only version you can pin is the one your provider exposes.

The safeguard split is the interesting part. When Fable refuses a cyber-adjacent query, the user is routed to Opus 4.8; biology refusals land on Opus 5. That fallback is visible to the caller, which means your error handling and your prompt design both need to account for "the model answered, but a different model answered."

![Two names, one set of weights meme](https://i.imgflip.com/b5jvwg.jpg)

## The price is the real headline

Fable 5.2 lists at $8 per million input tokens and $40 per million output tokens. Fast mode is $16 and $80. That's a 2x multiplier for latency, which is a very different trade from the cached-input discounts you might be used to on other providers.

Context is listed at 1M tokens. If you're coming from a 200K-context model, the temptation is to stuff everything in and let the input price absorb it — at $8 per million input, a single 500K-token request costs roughly $4 before the model generates a token. That math is fine for a batch enrichment job and alarming for a chat turn.

Anthropic still recommends Opus 5.5 as the daily driver. That guidance matters if you were planning to swap a default model name and call it a migration.

## What Azure and Foundry engineers should pin

There's no Microsoft-first-party announcement of this launch in the current record, so treat the Azure availability as provider-listed rather than Microsoft-heralded. Practically:

- Pin `claude-fable-5-2` explicitly. Do not assume a Foundry alias resolves to the new version, and do not assume a bare `claude-fable` alias keeps pointing where it did.
- Decide Fast mode per call site. At $16/$80, it belongs behind a feature flag or a latency-sensitive code path, not as a global default.
- Handle the safeguard fallback. If your application logs model responses, log which model actually produced them. A silent downgrade to Opus 4.8 is exactly the kind of thing that shows up in a quality regression six weeks later.
- Check your retirement calendar separately. Foundry's published dates for `claude-haiku-4-5` (November 15) and Opus 4.5 (November 24) don't line up with Anthropic's own deprecation page, which still lists `claude-haiku-4-5-20251001` as active with "not sooner than October 15, 2026." When two vendors publish different dates for the same model, the safe assumption is the earlier one for planning and the later one for optimism.

## The broader pattern worth noticing

This release is one data point in a trend: model providers are shipping the same weights under multiple policy envelopes, then charging differently for capability tiers. Gemini 4 Argon's introductory pricing ends December 1, Cursor is losing OpenAI models on November 12, and OpenAI's image API lineup shifts to `gpt-image-2.5-sunburst` and `gpt-image-2.5-flare` on December 1 as `gpt-image-1-mini`, `gpt-image-1.5`, and `chatgpt-image-latest` retire. A model name in a config file is now a short-lived artifact.

The defensive move is boring: keep model IDs in configuration rather than code, version them alongside deployments, and log the resolved model on every response. When your provider splits a model in two and doubles the fast-tier price, you want that change to be a config diff and a cost dashboard spike — not a mystery.

## Further reading

https://www.marktechpost.com/2026/11/10/anthropic-claude-fable-5-2-mythos-5-2-release/
https://www.anothercodingblog.com/p/another-daily-ai-newsletter-november-b6c
https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026
https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure
https://enjoy.dev/blog/ai-models-shutting-down-november-2026/
https://qiita.com/ManabuYamamoto/items/7b05e77c82648ef3a2f1
https://aibusinessweekly.net/p/ai-pricing-index
https://www.promptzone.com/ai-model-releases