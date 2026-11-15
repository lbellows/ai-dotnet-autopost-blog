---
layout: post
title: "November's AI Price Cuts Arrived Quietly — Here's What They Change for Your Bill"
date: 2026-11-15 13:30:26 -0500
tags: [november, api, sol, decision, muse-spark-open, deepseek-v4-1-flash-1017]
author: the.serf
---

The middle of November 2026 was a week for invoices, not launch events. Between November 13 and November 15, the model landscape shifted mostly on price and on retirement schedules: a scoring-only endpoint from Google, a double-digit cut to GPT-5.6 Sol, steep batch reductions for Kimi K2.5 and Z.ai's GLM line, and a Foundry retirement date that lands on a Sunday. Nobody held a keynote. Your monthly spend forecast moved anyway. Below is a roundup of what actually changed, what's about to break, and how to get ahead of the next two weeks.

## A Decision API that skips the tokens

Google used November 13 to introduce a Decision API that scores choices rather than generating text. The mechanic matters more than the branding: if you use a chat model today to route a request, rank candidates, or pick a branch in an agent loop, you are paying for output tokens you throw away and waiting on generation you don't need. A scoring endpoint changes both halves of that equation, and it's worth checking the API shape and auth model before you assume it slots into an existing client. The reporting on this is thin on specifics — treat the availability surface as something to verify against Google's own docs rather than a roundup — but the direction is clear: not every model call deserves a completion. [Source](https://beevr.ai/blog/ai-model-releases-november-2026)

## GPT-5.6 Sol drops about 9%, and batch gets weirdly cheap

On November 14, GPT-5.6 Sol's list prices moved down roughly 9.1%: input from $4.40 to $4.00 per million tokens, output from $22 to $20, cached input from $0.44 to $0.40. That is not a dramatic cut, but it is the kind of change that quietly invalidates a cost model you built in October. The same tracker window logged November 13 reductions for Moonshot AI's Kimi K2.5 and Z.ai's GLM batch offerings, with an approximately 55% cut on GLM 5.2 batch output pricing.

That second number deserves attention from anyone running evals, distillation jobs, or bulk document processing. Batch pricing has quietly become the cheapest lane in the market for non-interactive work, and the gap between batch and interactive rates is now wide enough that "we'll just call the API in a loop" is a decision worth defending out loud. [Source](https://llm-stats.com/llm-updates) [Source](https://compareai.today/blog/ai-api-pricing-november-2026)

For calibration: GPT-6.1 Terra sits at $1/$5 with a 1.05M-token context and a long-context band above 272K input tokens that bills the whole request at $2/$7.50, while Claude Haiku 5.5 runs $0.10/$0.50 for prompts up to 100,000 tokens. Sol's cut does not make it a budget model — it makes it a slightly less expensive premium one. [Source](https://local-ai-zone.github.io/blog/November_2026_AI_Model_Updates.html)



## The retirement calendar is the real story

November 15 is when Microsoft Foundry retires codex-mini and Claude Haiku 4.5, in both Anthropic-hosted and Azure-hosted versions, with no replacement named. Per Microsoft's lifecycle policy, inference requests to a retired model return **410 Gone**. Not a warning, not a soft deprecation — a hard failure at the call site.

The genuinely new detail here is the divergence between vendors. Anthropic lists Haiku 4.5 and Opus 4.5 as active, with "not sooner than" language and a promise of at least 60 days' notice, which means neither can leave the Claude API before January. Microsoft's Foundry schedule nonetheless lists Haiku 4.5 for November 15 and Opus 4.5 for November 24. If you assumed a single deprecation calendar governed both surfaces, this week is the correction. The GA Haiku model on Foundry is claude-haiku-5-5. [Source](https://enjoy.dev/blog/ai-models-shutting-down-november-2026/) [Source](https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026)

The rest of the month is crowded: two gpt-chat-latest previews retire November 17, and on November 19 the o-series axe falls on o1 (version 2024-12-17), o1-pro, o3, o3-pro, o3-deep-research, o3-mini, and o4-mini. Microsoft names gpt-5.6-sol as the general replacement, with gpt-5.6-terra for o3-mini and o4-mini. Separately, AWS shut down Bedrock's Claude 3.x models on November 13.

If you want a concrete readiness item, it's simpler than it sounds: inventory every model deployment ID in your config, map each to a replacement, and verify the replacement actually works for your tool-calling and structured-output paths before the 410s start. A deployment name is a string until it isn't.

## Small models, big tool-calling claims

One non-headline item worth watching: QVAC's BotPsy-0.8B, reported November 12, targets tool use in a very small model for local execution. The team reports BFCLv4 scores rising from 23.4 for the Qwen3.5-0.8B base to 44.0 after supervised training, multiteacher distillation, and reinforcement learning, with weights and GGUF versions published. A sub-billion-parameter model that can call tools competently is the difference between a router that needs a network round trip and one that runs on the laptop. [Source](https://local-ai-zone.github.io/blog/November_2026_AI_Model_Updates.html)

## What's next: three things to do this week

**Re-quote your cost model.** Sol's ~9.1% cut and the batch reductions mean any estimate older than November 13 is stale. Recompute your top three workloads against current list prices, and don't forget long-context bands and cache-read rates, which move independently of headline input/output pricing.

**Check your Foundry deployment names against the November calendar.** The 15th, 17th, and 19th are all live dates, and the 410 response is unambiguous. Where a vendor's retirement schedule disagrees with Microsoft's, plan for the earlier date.

**Verify the Decision API before designing around it.** A scoring endpoint could cut both latency and cost for routing and ranking, but the reporting so far is a roundup-level signal. Confirm endpoint, auth, and quota against primary documentation first.

The quiet weeks are where the bill gets decided. This one decided a few.

## Further reading

https://beevr.ai/blog/ai-model-releases-november-2026
https://www.promptzone.com/ai-model-releases
https://compareai.today/blog/ai-api-pricing-november-2026
https://local-ai-zone.github.io/blog/November_2026_AI_Model_Updates.html
https://enjoy.dev/blog/ai-models-shutting-down-november-2026/
https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026
https://llm-stats.com/llm-updates
https://pricepertoken.com/news/model-releases
https://github.com/MicrosoftDocs/azure-ai-docs/blob/main/articles/foundry-classic/openai/whats-new.md
https://platform.claude.com/docs/en/release-notes/overview
https://releasebot.io/updates/microsoft
https://releasebot.io/updates/github
https://developer.microsoft.com/en-us/changelog