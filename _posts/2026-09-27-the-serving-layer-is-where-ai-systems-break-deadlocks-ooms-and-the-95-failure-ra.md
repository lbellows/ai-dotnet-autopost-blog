---
layout: post
title: "The Serving Layer Is Where AI Systems Break: Deadlocks, OOMs and the 95% Failure Rate Nobody Paged"
date: 2026-09-27 12:28:30 -0400
tags: [azure, digest, litellm, serving, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

Ask an AI engineering team where their incidents come from and you'll usually hear about prompts, retrieval, or a model that got quietly swapped under them. Ask the people actually keeping inference online and you'll get a different list: a deadlock that only appears when FP8 meets prefix caching, an out-of-memory kill during long prefill on unified memory, a proxy that mangles multipart bodies, and a cloud tier that failed roughly 95% of requests across every model it served.

That last one is not a metaphor. In the serving-engine roundup published on 2026-09-27 — covering vLLM, SGLang, llama.cpp, Ollama, LiteLLM and Unsloth — Ollama Cloud Pro is reported at a 95% failure rate across all cloud models, tracked as issue #15453. Whatever your opinion of the model leaderboards this month, that number is the one that should be on a dashboard.

## The failures that don't care how good your prompt is

Two items from the same roundup deserve attention from anyone running inference at scale rather than against a hosted API.

The first is a vLLM V1 engine deadlock under concurrent load, triggered by the combination of FP8 quantization, prefix caching, and Qwen3.5 (issue #37729). Thirty-six comments, no fix at the time of the digest. This is the classic shape of a serving bug: each ingredient is individually sane, the interaction is not, and it only manifests once you have real concurrency — which is exactly when you can least afford to debug it.

The second is subtler and arguably worse. Speculative decoding corrupts multi-token `reasoning_end_str` handling when V1's thinking budget is enabled (issue #58485). Nothing crashes. Your agent just stops reasoning in the right place, and you spend an afternoon blaming the model.

![The Serving Layer Is Where AI Systems Break: Deadlocks, OOMs and the 95% Fail...](https://i.imgflip.com/b24yw7.jpg)

## Unified memory is not a free lunch

On the GB10 unified-memory boxes that have become popular for local work, a Qwen4Exp QSA indexer OOM (issue #56457) traces back to a per-chunk logits buffer that grows with `max_seq_len`. Long prefill, device OOM or hang, and a hardware class that looks generous right up until the sequence length gets serious.

If you are sizing an inference box — and plenty of teams are, given how cheap local serving has become — the lesson is that VRAM estimates and unified-memory estimates are not interchangeable. A separate regression in the 2026-09-25 digest makes the same point from the other direction: `gemma4:31b` throughput reportedly fell from 33.8 to 4.7 tok/s after v0.31.2 because inflated VRAM estimates pushed work into a worse memory path (issue #17099). A seven-fold slowdown from an estimator change. No model was retrained, no prompt was edited.

## Latency grows with context, and the padding grows with it

The same 2026-09-25 digest flags a Triton backend behavior worth knowing about: padded decode CUDA-graph slots get progressively more expensive as context lengthens, roughly 27.7 ms/token at 30K tokens rising to 35.9 ms/token (issue #41151). Padded slots are a throughput optimization that quietly becomes a latency tax at long context. On the positive side, deferred FFN all-reduce work across DeepSeek models (PRs #41191–#41193) targets redundant computation and memory pressure, and prefill throughput on DeepSeek-V4 across four RTX PRO 6000 cards on SM120 is reported in the 2–7K tok/s range.

## Two LiteLLM fixes that matter more than they look

If you put LiteLLM in front of your models — and a lot of teams do, precisely so they can swap providers without touching application code — two PRs from the 2026-09-27 digest are worth tracking:

- **PR #11589** enables `text/event-stream` responses when the client asks for them, which is the difference between streaming working and streaming silently buffering behind a proxy.
- **PR #18670** fixes multipart request handling by forwarding raw bodies unchanged, explicitly called out as critical for tools like Codex Desktop.

The second one is the kind of bug that produces a support ticket reading "image uploads work in the browser but not in the agent." Forwarding a body unchanged sounds like a no-op. It is not, once anything in the chain has opinions about multipart encoding.

## What this means for .NET and Azure teams

Two practical consequences, both boring and both important.

**First, your gateway is a product, not plumbing.** Managed hosting via Azure AI Foundry is not immune — the failure modes shift from deadlocks to quota, regional processing, and model-version drift — but self-hosted serving engines put all of the above directly in your incident rotation. Whichever side you land on, the observability you need is the same: per-token latency as a function of context length, streaming integrity checks on every proxied endpoint, and an alert on cloud-tier error rate that fires long before 95%.

**Second, pin everything you can pin, and re-measure when you unpin.** The `gemma4:31b` regression and the FP8/prefix-caching deadlock share a root cause in practice: a version bump or a config flag changed an interaction nobody was watching. Serving stacks move fast, and release notes are frequently thinner than the change they describe — the Codex CLI's 0.157.0 release on 2026-09-25 is a good example of a version landing while its notes failed to load.

If you want one thing to take away: model quality debates are cheap and serving reliability is expensive. Track the issues, pin the versions, and measure throughput at your real context length rather than the one in the benchmark.

## Further reading

- https://github.com/duanyytop/agents-radar/issues/3512 — AI Infrastructure Digest, 2026-09-27 (vLLM, SGLang, llama.cpp, Ollama, LiteLLM, Unsloth)
- https://github.com/duanyytop/agents-radar/issues/3478 — AI Infrastructure Digest, 2026-09-25 (Triton latency, memory regression)
- https://github.com/datnguyenquy94/news-radar/issues/591 — AI CLI Tools Digest, 2026-09-25
- https://github.com/openai/codex/releases — openai/codex releases (0.157.0)
- https://github.com/managedcode/dotnet-skills/releases/tag/catalog-v2026.9.25.0 — managedcode/dotnet-skills Catalog 2026.9.25.0
- https://github.com/microsoft/agent-framework/releases — microsoft/agent-framework releases
- https://github.com/Azure/azure-dev/pull/9958 — azd Foundry extension / azure.ai.agents 1.0.0-beta.15
- https://github.com/Azure/azure-sdk/pull/10234 — Azure SDK .NET release notes, 2026-09 cycle
- https://github.com/nicholasdbrady/foundry-docs/issues/1042 — Azure AI Projects SDK v2 release tracking
- https://learn.microsoft.com/en-us/azure/foundry-classic/openai/whats-new — What's new in Azure OpenAI in Microsoft Foundry Models (classic)
- https://releasebot.io/updates/github — GitHub release notes aggregator, September 2026
- https://releasebot.io/updates/microsoft — Microsoft release notes aggregator, September 2026
- https://developers.openai.com/api/docs/changelog — OpenAI API changelog