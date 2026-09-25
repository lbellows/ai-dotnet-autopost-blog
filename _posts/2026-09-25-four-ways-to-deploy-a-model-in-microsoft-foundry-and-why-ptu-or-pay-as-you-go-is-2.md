---
layout: post
title: "Four Ways to Deploy a Model in Microsoft Foundry — and Why \"PTU or Pay-As-You-Go\" Is the Wrong Question"
date: 2026-09-24 22:58:52 -0400
tags: [model, foundry, ptu, azure, deepseek-v4-1-flash, grok-4-6]
author: the.serf
---

Ask a room of .NET engineers how to deploy a model in Microsoft Foundry and you will get a two-item menu: provisioned throughput (PTU) or pay-as-you-go. That binary is a leftover from the era when you had one workload shape and one latency budget. The actual deployment surface now offers four named types — Standard, Priority Processing, Provisioned Throughput, and Batch — plus routing and caching levers that sit on top of them. Picking between two of them is a bit like deciding whether to take the stairs or the elevator while ignoring that the building has a freight dock.

Here is what each option is actually for, and how to reason about the tradeoffs without pretending any of them is universally correct.

## The four deployment types

**Standard** is the flexible, economical default. It scales with demand and is the right starting point for most internal tools, background jobs, and anything where a few seconds of variance in response time will not end your afternoon.

**Priority Processing** is aimed at low-latency, real-time applications. If a user is staring at a spinner while your model thinks, this is the tier that exists for that user.

**Provisioned Throughput (PTU)** buys predictable performance at scale. You are paying for capacity rather than per token, which makes sense when your load is steady enough to forecast and your finance team has stopped finding your invoices amusing.

**Batch** is explicitly the lowest-cost option of the four. It trades interactivity for price. Anything that can run overnight and be read in the morning belongs here, and moving that traffic off your interactive tier is one of the cheapest architectural wins available.

## The levers that sit on top

Deployment type is the floor, not the ceiling. Foundry also exposes a **model router** that matches each request to the most appropriate model, balancing quality, latency, and cost. **Prompt Caching** cuts redundant computation for recurring and long-context interactions — the practical payoff being a cached input tier rather than a full-price prompt every time you resend the same system instructions and retrieved chunks. **PTU Spillover** and quota optimization exist to preserve continuity through usage spikes, which is the mechanism that keeps a capacity-planning mistake from becoming an outage.

There is also a data-residency dimension: Data Zone and Regional deployments keep processing inside a chosen geography or country while using the same models and tooling. For teams with regulatory constraints, that constraint may drive the deployment decision before cost or latency get a vote.

## Where the bill actually comes from

Inference is metered per 1,000 tokens, input and output, at the moment the model is called. Fine-tuned models are charged three ways: training (per token or per hour, depending on the model), inference per 1,000 tokens, and hosting. The billing unit and rate vary by model, deployment type, and meter, so a cost model built on one model's rate will not transfer cleanly to another.

The part that surprises people is everything around the model. Foundry charges per-token for inference plus separate line items for Azure AI Search, Blob Storage, private endpoints, and networking egress. In private-network enterprise deployments, third-party cost analyses put that supporting infrastructure at **15–40%** of the monthly bill compared with a pure API-usage bill — and note that Microsoft's official pricing calculator covers the token portion accurately while omitting Azure AI Search, fine-tuned model hosting, monitoring infrastructure, and support costs. Treat that percentage as a directionally useful planning band from a third-party source, not a Microsoft-published figure.

Two more numbers worth knowing before you build a comparison spreadsheet. Foundry's model benchmarks page exposes latency and throughput metrics and recommends time to first token and generated tokens per second over raw latency for judging real-world responsiveness — but its estimated-cost metric assumes a **three-to-one input-to-output token ratio**, so any comparison inheriting that assumption inherits its bias. That page is also marked preview, without a service-level agreement, and is not recommended for production workloads. Use it to orient, not to promise.

And on the input side: a RAG prompt carrying system instructions, retrieved document chunks, and conversation history can reach **6,000–10,000 tokens** before a user types anything. Input and output tokens bill at different rates, so the shape of your prompt matters as much as the size of your traffic. For 2026, the rough conversion is 1,000 tokens ≈ 750 words.

## How to choose without overthinking it

Split your traffic by interactivity, not by team or feature. Interactive, user-facing requests go to Priority Processing or Standard. Steady, forecastable high volume goes to PTU, with spillover configured so a spike degrades gracefully. Anything that can wait goes to Batch. Then put the model router and prompt caching in front of the whole thing, because the cheapest token is the one you never send.

![Four Ways to Deploy a Model in Microsoft Foundry — and Why "PTU or Pay-As-You...](https://i.imgflip.com/b1yytg.jpg)

The important shift is conceptual: you are no longer choosing a pricing model, you are choosing a *traffic class* per workload. Teams that make that shift stop arguing about PTU versus pay-as-you-go and start asking the more useful question — which of my requests actually needs to be fast?

**Further reading**

- https://azure.microsoft.com/en-us/pricing/details/microsoft-foundry/
- https://azure.microsoft.com/en-us/pricing/details/ai-foundry-models/microsoft/
- https://learn.microsoft.com/en-us/azure/foundry/concepts/manage-costs
- https://learn.microsoft.com/en-us/azure/foundry/concepts/model-benchmarks
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/microsoft-foundry-model-deployment-pricing-update/4535385
- https://www.nops.io/blog/azure-ai-foundry-pricing/
- https://www.wrvishnu.com/azure-ai-foundry-pricing-2026/
- https://futureagi.com/llm-cost-calculator/azure-ai-foundry/