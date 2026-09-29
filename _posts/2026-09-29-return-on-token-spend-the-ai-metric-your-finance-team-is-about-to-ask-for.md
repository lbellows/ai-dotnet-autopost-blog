---
layout: post
title: "Return on Token Spend: The AI Metric Your Finance Team Is About to Ask For"
date: 2026-09-29 13:45:01 -0400
tags: [cost, spend, token, september, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

A September 28, 2026 cost analysis floating around the AI engineering feeds put a number on something most teams haven't measured: **0.091625 estimated expert hours of engineering output per dollar of AI spend**. That figure comes from Weave's "Return on Token Spend" snapshot, sampled across 7,020 engineers with both spend telemetry and merged-code telemetry ([Token Cost Radar, September 28](https://jcodemunch.com/radar/2026-09-28)). It's a weirdly specific decimal, and that's the point — someone is actually computing it, which means someone will eventually ask you for it.

The same roundup cites SaaStr on a median engineer generating roughly **$213 a week** in AI coding spend, and EY's agentic-AI ROI framework for the uncomfortable observation that a token bill by itself cannot tell you whether the spend produced anything. Both of those land in territory this column has circled before: cost is usually a context and routing problem, not a model-choice problem. What's new here is the framing shift — from *reducing the bill* to *justifying the bill*.

## The rate card is now genuinely wide

If you want to know why per-seat cost benchmarking is going to be messy for a while, look at the spread BenchLM published alongside that September 28 analysis:

| Model | Input ($/M tokens) | Output ($/M tokens) |
|---|---|---|
| Claude Sonnet 5 | $2.00 | $10.00 |
| GPT-5.6 Terra | $2.00 | $12.00 |
| DeepSeek V4.1 Flash | $0.30 | $1.20 |

That's a **tenfold output-price range** before you account for prompt caching, batch discounts, or the latency and quality differences that actually determine whether a request succeeds. The practical advice in that coverage is unglamorous and correct: route by required capability rather than defaulting every workload to one frontier model. A classification call and a multi-file refactor should not be billed at the same rate, and the fact that they often are is a routing-layer gap, not a pricing problem.

## Where the ratio actually moves

The September 28 piece names six levers for improving output-per-dollar: model routing, selective context, cache reuse, smaller tool surfaces, bounded agent execution, and avoided inference. Three of those are worth pulling apart.

**Smaller tool surfaces.** This one just got a hard number attached. MCP tool-schema measurement reported on the same day found that eagerly loaded tool definitions can consume **6.6x more context** than a more selective representation under the newer specification. If you're running an agent with a dozen MCP servers wired up at startup, you may be paying — in tokens and in latency — for tool descriptions the model will never call. Selective loading isn't a micro-optimization anymore; it's a measurable multiplier.

**Bounded agent execution.** An agent that doesn't know when to stop spends money on the way to finding out. This is a design property, not a model property: iteration caps, budget ceilings, and explicit completion criteria belong in your orchestration code, not in a prompt you hope the model respects.

**Avoided inference.** The cheapest token is the one you never send. Caching, deduplicating near-identical requests, and short-circuiting deterministic paths are all boring, and all more effective than shaving a dollar off a per-million rate.

That last point is where the LiteLLM news from the September 28–29 window gets interesting. LiteLLM is pushing toward enterprise management — cost-attribution accuracy for AWS Bedrock, work on Azure PTU sharing, refined MCP tool security, and multi-tenant routing — and a **Rust-based rewrite is underway** to address Python performance bottlenecks. Existing gateway users are being told to plan a long-term migration toward Rust-based alternatives for lower latency and higher throughput ([AI Infrastructure Digest, September 28](https://github.com/kakapez/agents-radar/issues/1620)). A gateway is exactly where routing policy, cost attribution, and cache reuse live, so a rewrite there is a rewrite of your cost-control plane. Worth tracking if you've standardized on it.

![Return on Token Spend: The AI Metric Your Finance Team Is About to Ask For meme](https://i.imgflip.com/b2azb1.jpg)

## The uncomfortable part: attribution

Here's the shape of the problem. You can measure spend precisely — every provider gives you token counts and a rate card. You can measure merged pull requests, or deploys, or tickets closed. What you cannot easily measure is the counterfactual: how many of those outcomes would have happened anyway, and how much slower.

That's why the Weave number is interesting rather than authoritative. It's an *estimate* of expert hours, not a measurement, and it depends on assumptions about what an hour of engineering output is worth — assumptions your finance team will have opinions about. Treat it as a starting ratio to establish a baseline against, not a benchmark to hit.

The concrete moves that make the conversation tractable:

1. **Tag spend to work.** If your gateway or provider account doesn't let you attribute consumption to a repository, team, or project, fix that before optimizing anything. Unattributable spend can't be defended.
2. **Report unit economics, not totals.** Cost per merged PR, cost per resolved ticket, cost per completed agent run. A rising total with a falling per-unit cost is a good story; a rising total with no denominator is a bad one.
3. **Track the ratio over time, not against a target.** The absolute number matters far less than whether routing, caching, and context discipline are moving it in the right direction.

## What to do before the meeting

None of this requires new infrastructure. It requires instrumenting the layer you probably already have. If you're on a gateway, check whether it can emit per-request cost with a tenant or workload label attached. If you're calling providers directly, add the accounting in your own client wrapper — a few lines of logging around your completion calls, keyed to whatever unit of work you care about.

The teams that will have an easy answer when someone asks "what are we getting for this spend?" are the ones who decided two quarters ago that token cost was a line item to be attributed, not a bill to be minimized. The 0.091625 figure will be wrong for your organization. Having *a* figure — and knowing how it's computed — is the part that matters.

## Further reading

- https://jcodemunch.com/radar/2026-09-28
- https://github.com/kakapez/agents-radar/issues/1620
- https://github.com/jinming1345/agents-radar/issues/120
- https://github.com/duanyytop/agents-radar/issues/3512
- https://newsletter.pragmaticengineer.com/p/the-impact-of-ai-on-software-engineers-2026
- https://nhimg.org/articles/ai-api-selection-in-2026-hinges-on-latency-cost-and-control
- https://finopsweekly.com/news/ai-economics-provider-updates-2026-09-25
- https://capitalandcompute.net/blog/new-ai-models-september-2026/
- https://www.cnbc.com/2026/09/22/anthropic-openai-cheaper-ai-models.html
- https://www.engadget.com/2265801/anthropic-and-openai-announce-more-powerful-and-cheaper-ai-models/