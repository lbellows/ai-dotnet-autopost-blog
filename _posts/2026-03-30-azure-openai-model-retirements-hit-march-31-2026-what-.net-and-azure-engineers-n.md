---
layout: post
title: "Azure OpenAI Model Retirements Hit March 31, 2026 — What .NET and Azure Engineers Need to Do *Now*"
date: 2026-03-30 08:04:44 -0400
tags: [deployments, .net, azure, affected, bigger, gpt-5.2-chat]
author: the.serf
---

## TL;DR
Several Azure OpenAI model versions are being **retired on March 31, 2026**. If your .NET or Azure apps still reference older `gpt-4o` or `gpt-4o-mini` deployments, you risk **service disruption**. Migrate to the replacement models (`gpt-5.1`, `gpt-4.1-mini`) immediately, test latency/cost changes, and update your deployment configs before the deadline.

---

## The narrowly focused news: Azure OpenAI model retirements (hard deadline: March 31, 2026)

Microsoft is retiring multiple **Azure OpenAI base model versions** at the end of March 2026. Auto-upgrades began earlier in the month, but **March 31, 2026 is the hard cutoff**—after that, requests to the retired models will fail. This is not a preview nuance or a doc-only cleanup; it directly affects running production workloads. ([github.com](https://github.com/ahmadabdalla/agents/issues/42))

### Affected models (most common in production)
- **`gpt-4o-mini` (2024-07-18)**  
  → Replacement: **`gpt-4.1-mini` (2025-04-14)**
- **`gpt-4o` (2024-05-13, 2024-08-06)**  
  → Replacement: **`gpt-5.1` (2025-11-13)**

Fine-tuning on the retired base models is no longer allowed after March 31, though existing fine-tuned deployments continue for a limited grace period. ([github.com](https://github.com/ahmadabdalla/agents/issues/42))

---

## Why this matters to .NET and Azure teams

### 1. **This is a runtime break, not a compile-time one**
Your C# code will happily build and deploy while still pointing at a retired deployment name. The failure only shows up at runtime—usually as a 404 or deployment-not-found error from Azure OpenAI. If you have environment-specific `appsettings.Production.json` files, this is where bugs like to hide.

### 2. **Latency and cost characteristics may change**
Microsoft positions the replacement models as functional successors, not bit-for-bit replicas. In practice:
- `gpt-4.1-mini` generally preserves the low-latency profile of `gpt-4o-mini`, but token pricing and throughput limits can differ.
- `gpt-5.1` is more capable than older `gpt-4o` builds, which may **increase per-request cost** if you don’t adjust max tokens or system prompts.

Microsoft’s “What’s new” guidance emphasizes validating performance and cost before switching production traffic. ([learn.microsoft.com](https://learn.microsoft.com/en-us/azure/foundry-classic/openai/whats-new))

---

## Concrete migration steps (do these today)

### Step 1: Inventory deployments
In the Azure Portal or via CLI, list your Azure OpenAI deployments and note the **model version**, not just the friendly name.

```bash
az cognitiveservices account deployment list \
  --name <openai-account> \
  --resource-group <rg>
```

### Step 2: Create replacement deployments
Create new deployments using the recommended models (`gpt-4.1-mini`, `gpt-5.1`) **side by side** with the old ones so you can A/B test.

### Step 3: Update .NET configuration
Most .NET apps wire the deployment name via configuration:

```json
// appsettings.json
{
  "AzureOpenAI": {
    "DeploymentName": "gpt-5-1-prod"
  }
}
```

If you’re using `Microsoft.Extensions.AI`, the abstraction stays the same—the **deployment name is the contract**, not the model ID. This is good design paying dividends. (You can pat your past self on the back.)

### Step 4: Run real workload tests
Replay representative prompts:
- Measure **p95 latency**
- Compare **token usage**
- Watch for subtle output differences (especially in structured JSON or tool-calling flows)

### Step 5: Remove or disable retired deployments
After validation, delete or lock down the old deployments so nobody accidentally routes traffic back to them.

---

## Operational gotchas to watch for

- **Auto-upgrade ≠ validation**: Some deployments were auto-upgraded earlier in March, but Microsoft still expects you to test and explicitly migrate. ([github.com](https://github.com/ahmadabdalla/agents/issues/42))
- **Multi-region apps**: If you deploy Azure OpenAI in multiple regions, verify each region’s deployment mapping. Recent availability incidents show region-specific behavior is still a thing. ([status.ai.azure.com](https://status.ai.azure.com/))
- **Documentation drift**: Update internal runbooks and architecture diagrams. Future-you will appreciate it.

---

## The bigger (but still practical) picture

These retirements reinforce a pattern Azure engineers should plan for: **model version churn is now part of normal cloud operations**, closer to TLS certificate rotation than a once-a-year upgrade. Treat model deployments as replaceable infrastructure, not long-lived pets.

If you already use configuration-driven deployment names and integration abstractions in .NET, this migration is mostly operational. If not—well, congratulations on discovering your next tech-debt story.

---

## Further reading
- https://github.com/ahmadabdalla/agents/issues/42  
- https://learn.microsoft.com/en-us/azure/foundry-classic/openai/whats-new  
- https://status.ai.azure.com/  
- https://devblogs.microsoft.com/dotnet/category/ai/