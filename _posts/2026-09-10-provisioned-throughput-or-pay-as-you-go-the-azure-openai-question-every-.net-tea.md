---
layout: post
title: "Provisioned Throughput or Pay-As-You-Go? The Azure OpenAI Question Every .NET Team Eventually Asks"
date: 2026-09-10 11:56:24 -0400
tags: [model, agent, azure, api, grok-4-6, claude-sonnet-5]
author: the.serf
---

Every team that ships an LLM feature on Azure eventually hits the same wall: the demo worked beautifully on pay-as-you-go pricing, and then a real customer sent real traffic, and now someone is asking why P95 latency looks like a dial-up handshake. The answer usually isn't "pick a bigger model." It's understanding how Azure OpenAI's deployment types, the Responses API, and a few underrated SDK changes interact — and that's worth walking through properly instead of discovering it during an incident review.

## Deployment type is a latency decision, not just a billing decision

Azure OpenAI doesn't give you one dial — it gives you several: Global Standard, Data Zone Standard, Regional Standard, Batch, Priority, and Provisioned Throughput Units (PTUs). Each one changes routing, data residency, and — critically — the consistency of your latency, not just the price per token. Pay-as-you-go tiers share capacity across tenants, which is a polite way of saying you can experience "noisy neighbor" slowdowns when someone else's batch job spikes. PTUs reserve dedicated throughput, usually with a monthly or yearly commitment, trading variable spend for predictable capacity.

If your service has a latency SLA, that's the first thing to nail down — before you argue about which GPT-5.1 variant to call. Pricing on the Azure OpenAI page spans roughly $0.05 per million input tokens on the smallest tiers up to $120 per million output tokens on the largest reasoning models, so the deployment-type decision and the model-size decision compound each other fast.

## Agent cost control is now a design pattern, not an afterthought

The more interesting shift for teams building agentic workloads is that Microsoft's guidance has moved past "just pick a cheaper model" toward actual context-engineering discipline: selective retrieval, tool search, reusable skills, and memory management as first-class cost levers. That tracks with what a lot of teams already learned the hard way — an agent that re-reads its entire conversation history on every tool call, or re-fetches a schema it already has, will burn tokens faster than any model-pricing change can save you. If you're building on Foundry Agent Service or the Assistants/Responses API, treat "how much context do I actually need to send" as a design review question, not a production surprise.

![Provisioned Throughput or Pay-As-You-Go? The Azure OpenAI Question Every .NET...](https://i.imgflip.com/b0u5lq.jpg)

## The Responses API breaking change that quietly reshaped client code

If you're on the .NET side, there's a concrete API surface change worth knowing about even outside the current news cycle: the Azure SDK for .NET removed the older model-and-deployment-based constructor overloads in favor of `CreateResponseOptions` carrying an explicit `Model` property, and the experimental Realtime Beta support in `Azure.AI.OpenAI` was dropped in favor of the standalone OpenAI library. If you migrated an older prototype, this is the shape of the change:

```csharp
using Azure.AI.OpenAI;
using OpenAI.Responses;
using Azure.Identity;

// Before (older SDK): client tied to a deployment at construction time
// var client = new OpenAIClient(endpoint, credential, deploymentName);

// After: OpenAIResponseClient built from AzureOpenAIClient, model set per-call
AzureOpenAIClient azureClient = new(
    new Uri("https://your-resource.openai.azure.com/"),
    new DefaultAzureCredential());

OpenAIResponseClient responseClient = azureClient.GetOpenAIResponseClient(deploymentName: "gpt-5.1-mini");

CreateResponseOptions options = new()
{
    Model = "gpt-5.1-mini", // explicit, not baked into client construction
};

try
{
    OpenAIResponse response = await responseClient.CreateResponseAsync(
        userInputText: "Summarize this quarter's incident count by root cause.",
        options);

    Console.WriteLine(response.GetOutputText());
}
catch (RequestFailedException ex) when (ex.Status == 429)
{
    // PTU exhaustion or Global Standard rate limit — this is where your
    // retry/backoff policy and deployment-type choice actually matter.
    Console.Error.WriteLine($"Throttled: {ex.Message}");
}
```

The point isn't the syntax novelty — it's that `Model` is now an explicit, per-call option rather than something implicit in how you constructed the client. That makes it much easier to route different requests to different deployment types (say, Regional Standard for interactive chat, Batch for nightly summarization) from the same service, which is exactly the kind of flexibility you want once you're juggling PTUs and pay-as-you-go side by side. Alongside this, `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Workflows`, and `Microsoft.Agents.AI.OpenAI` moved to `1.0.0-rc1`, and the tracing provider name changed from `azure.ai.agents` to `microsoft.foundry` — worth grepping for if your observability dashboards still reference the old string.

## Practical takeaways

- Pick deployment type based on your latency SLA first, model size second. A cheap model on a congested shared tier can be slower than a pricier model on committed capacity.
- Budget agent token spend around context design — selective retrieval and reusable skills — not just model choice.
- If you're upgrading Azure SDK for .NET packages, search for old deployment-based client constructors and old `azure.ai.agents` tracing strings; both changed.
- Prompt-cache write billing has been a moving target — confirm current billing status before assuming cache writes are free in your cost model.

None of this requires waiting for the next flashy model announcement. The unglamorous work — matching deployment type to latency needs, controlling agent context growth, and keeping SDK usage current — is what actually keeps a production AI feature affordable and boring in the best possible way.

## Further reading

https://azure.microsoft.com/en-us/pricing/details/azure-openai/
https://www.truefoundry.com/blog/understanding-azure-ai-gateway-pricing-for-2026---a-complete-breakdown
https://www.cloudzero.com/blog/azure-openai-pricing/
https://benchlm.ai/azure/llm-pricing
https://finopsweekly.com/news/azure-updates-2026-09-10/
https://azure.github.io/azure-sdk/releases/2026-03/dotnet.html
https://learn.microsoft.com/en-us/dotnet/ai/dotnet-ai-ecosystem
https://azure.microsoft.com/en-us/blog/product/microsoft-foundry/
https://dev.to/amitesh0512/ai-orchestration-for-enterprise-net-applications-scaling-intelligent-agents-with-azure-3635