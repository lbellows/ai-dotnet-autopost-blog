---
layout: post
title: "Microsoft Foundry SDK 2.0 Lands: Agent APIs Consolidated, Faster Open-Model Inference, Fewer Packages to Babysit"
date: 2026-04-10 21:13:07 -0400
tags: [why, less, matters, one, them, gpt-5.2-chat]
author: the.serf
---

## TL;DR
Microsoft Foundry quietly shipped a **meaningful SDK cleanup and capability bump** in the last 48 hours. The **azure‑ai‑projects 2.0.0** release (with a matching **.NET 2.0.0**) collapses agent APIs into a single surface, removes extra dependencies, and pairs nicely with new **high‑performance open‑model inference** options (Fireworks AI) now visible in Foundry. If you’re shipping AI features on **.NET + Azure**, this is a “stop what you’re doing and skim the changelog” moment.

---

## The one story that matters this week
On **April 9–10, 2026**, Microsoft published the **“What’s new in Microsoft Foundry | March 2026”** update, and despite the month label, the impact is very current for engineers integrating Foundry today ([devblogs.microsoft.com](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-mar-2026/)). The headline change isn’t a flashy model—it’s **API consolidation** that directly affects how you build, test, and ship agentic apps.

### Why this is a big deal
Foundry’s SDK surface had started to sprawl: separate packages for projects, agents, identity, and OpenAI wiring. Version **2.0.0** tightens this up and makes agents a first‑class citizen—especially relevant if you’re building **multi‑agent workflows** or embedding Foundry into existing .NET services.

---

## What actually changed (and why you should care)

### 1) One client to rule them all
Agents are now **first‑class operations on `AIProjectClient`**. The standalone `azure-ai-agents` dependency is gone.

**Before (simplified):**
```csharp
using Azure.AI.Agents;
using Azure.Identity;

var agentClient = new AgentClient(endpoint, new DefaultAzureCredential());
```

**After (.NET SDK 2.0.0):**
```csharp
using Azure.AI.Projects;

var project = new AIProjectClient(endpoint, credential);
var agent = await project.Agents.CreateAsync(...);
```

Fewer abstractions, fewer packages, fewer version conflicts. Your CI pipeline will thank you.

([devblogs.microsoft.com](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-mar-2026/))

---

### 2) Dependency diet: install less, break less
As of **azure‑ai‑projects 2.0.0**, the Python package bundles `openai` and `azure-identity` directly; the .NET release mirrors the spirit by eliminating extra agent packages.

```bash
pip install azure-ai-projects
```

No more “why does this sample need four Azure AI packages?” moments.

([devblogs.microsoft.com](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-mar-2026/))

---

### 3) Open models, but make them fast
Foundry now exposes **Fireworks AI–hosted open models** (preview), including **DeepSeek‑V3.2**, **gpt‑oss‑120b**, **Kimi‑K2.5**, and **MiniMax‑M2.5**, with both **pay‑per‑token** and **provisioned throughput** options.

This matters because:
- Latency is competitive with proprietary APIs.
- You can deploy **post‑trained or fine‑tuned weights** without standing up your own inference stack.
- Pricing is explicit and predictable for production workloads.

([techcommunity.microsoft.com](https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/announcing-fireworks-ai-on-microsoft-foundry/4500950))



---

### 4) Model catalog keeps expanding (carefully)
The same update confirms:
- **NVIDIA Nemotron models** are now first‑class in the Foundry catalog.
- **Grok 4.2** has graduated to GA status.

This doesn’t mean “switch everything now,” but it does mean **model diversity without vendor lock‑in gymnastics** is improving.

([devblogs.microsoft.com](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-mar-2026/))

---

## Practical upgrade checklist for .NET teams

1. **Audit packages**
   - Remove `Azure.AI.Agents` if present.
   - Upgrade to the **Foundry .NET SDK 2.0.0**.

2. **Refactor client creation**
   - Centralize on `AIProjectClient`.
   - Treat agents as a project resource, not a sidecar.

3. **Re-evaluate inference choices**
   - If cost or latency is a pain point, trial a Fireworks‑hosted open model in a non‑prod environment.

4. **Lock versions intentionally**
   - This is a cleanup release, but it’s still a major version. Pin it, test it, then roll forward.

---

## Why this matters beyond this week
This update signals a clear direction: **Foundry is becoming the opinionated, stable control plane for Azure‑native AI apps**, not just a model menu. For .NET engineers, that translates to:
- Cleaner APIs
- Fewer surprise dependencies
- A smoother path from prototype to production

No hype—just less friction. And honestly, we could all use more of that.

---

## Further reading
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-mar-2026/
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/whats-new-in-foundry-labs---april-2026/4509714
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/announcing-fireworks-ai-on-microsoft-foundry/4500950
- https://techcrunch.com/2026/04/02/microsoft-takes-on-ai-rivals-with-three-new-foundational-models/