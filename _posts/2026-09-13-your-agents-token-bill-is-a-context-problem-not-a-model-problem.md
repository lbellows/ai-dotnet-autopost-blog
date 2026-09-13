---
layout: post
title: "Your Agent's Token Bill Is a Context Problem, Not a Model Problem"
date: 2026-09-13 11:43:17 -0400
tags: [context, agent, model, .net, grok-4-6, claude-sonnet-5]
author: the.serf
---

If your Azure OpenAI invoice has been creeping upward even though usage "feels" flat, the culprit is probably not the model you picked — it's everything you're stuffing into the context window before the model ever sees your prompt. Microsoft's own agent cost guidance boils down to four unglamorous levers: retrieve selectively, search for tools instead of loading them all, reuse skills instead of re-explaining them, and manage memory instead of replaying full history every turn. None of that is exciting, but it's the difference between a demo that works and a production agent that doesn't bankrupt you by Thursday.

## Why context, not tokens-per-dollar, is the real lever

It's tempting to obsess over headline pricing — GPT-5.1's adaptive reasoning is explicitly pitched as improving "latency and cost efficiency across the series by varying thinking time more significantly," which is genuinely useful. But most enterprise agent spend isn't driven by which model you chose; it's driven by how much context gets shipped on every single call. An agent with 40 tool definitions, a bloated system prompt, and full conversation replay pays the "context tax" on every turn, regardless of whether you're on a frontier model or a cheaper tier.

Three practical levers worth building into your architecture:

- **Selective retrieval** — don't hand the model your entire knowledge base; retrieve the slice relevant to the current turn.
- **Tool search over tool dump** — if your agent has dozens of tools, look up the relevant ones dynamically rather than declaring all of them in every request.
- **Reusable skills and memory management** — persist distilled state instead of replaying full transcripts, and treat memory as a summarization problem, not a storage problem.

## The billing wrinkle nobody reads until the invoice arrives

If you're calling models directly rather than through a managed layer, prompt caching has its own pricing quirk worth knowing: cache **writes** commonly bill at a multiplier over the input rate (roughly 1.25x on current models, per public cost-tracking writeups), while cache *reads* are the actual savings mechanism. On Azure, Global Standard pricing tends to track OpenAI's direct rates, Data Zone SKUs run modestly higher, and Regional deployments higher still — and production overhead (retries, logging, evaluation calls) typically adds another 20–40% on top of raw token math. If your bill jumps between two otherwise-identical months, check whether you crossed a Data Zone boundary or started caching writes you didn't intend to.

For steady, high-volume workloads, Provisioned Throughput Units (PTUs) convert unpredictable token spend into a fixed infrastructure cost and insulate you from "noisy neighbor" latency variance on shared pay-as-you-go capacity. For bursty or exploratory workloads, PTUs are the wrong tool — you'll pay for idle hours nobody used.

## What this looks like in .NET

The official `openai-dotnet` library supports Azure OpenAI with Entra ID auth out of the box, which is worth adopting anyway since it sidesteps key rotation. The failure path that actually matters in production is handling `429`/context-length errors gracefully rather than retrying blindly and multiplying your bill:

```csharp
using Azure.Identity;
using OpenAI.Responses;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var credential = new DefaultAzureCredential();
ResponsesClient client = new(endpoint, credential, "gpt-5-mini");

// Trim context before sending: keep only the last N turns and a summarized
// memory blob instead of replaying the entire conversation every call.
string trimmedHistory = ConversationMemory.SummarizeRecent(maxTurns: 6);

try
{
    var response = await client.CreateResponseAsync(
        input: trimmedHistory,
        cancellationToken: CancellationToken.None);

    Console.WriteLine(response.Value.GetOutputText());
}
catch (RequestFailedException ex) when (ex.Status == 429)
{
    // Rate/quota limit hit: back off, don't just replay the same
    // oversized context on retry — that's how a transient error
    // becomes a doubled bill.
    var delay = ex.GetRetryAfter() ?? TimeSpan.FromSeconds(5);
    await Task.Delay(delay);
    throw; // let a bounded-retry policy upstream decide, don't loop here
}
catch (RequestFailedException ex) when (ex.Status == 400)
{
    // Likely context-length exceeded — this is your cue to trim
    // harder, not to switch to a bigger-context model by reflex.
    throw new InvalidOperationException(
        "Context too large; check retrieval and memory trimming logic.", ex);
}
```

The point isn't the SDK call itself — it's that `ConversationMemory.SummarizeRecent` line. If your agent framework doesn't have an equivalent, you're paying to re-read your own conversation history on every turn.

If you're building agents natively in .NET rather than calling models directly, Microsoft's Agent Framework packages (`Microsoft.Agents.AI`, `Microsoft.Agents.AI.Workflows`, `Microsoft.Agents.AI.OpenAI`) reached `1.0.0-rc1` and bundle much of this context and memory management as first-class concerns rather than something you bolt on. The GitHub Copilot SDK for .NET (technical preview, last documented at v0.1.25) targets a narrower use case �� embedding Copilot's own agentic runtime for tool calling and multi-turn sessions inside a .NET app — and is worth a look if you'd rather borrow Copilot's plumbing than build your own.

![Your Agent's Token Bill Is a Context Problem, Not a Model Problem meme](https://i.imgflip.com/b11sev.jpg)

## Practical takeaways

- Audit what actually gets sent in your system prompt and tool schema on every call — most savings live there, not in model selection.
- Understand your cache-write multiplier before you architect around caching as a cost saver; verify current rates in the live Azure pricing calculator rather than trusting last quarter's numbers.
- Match your capacity model to your traffic shape: PTUs for steady high volume, pay-as-you-go for bursty or exploratory work.
- If you're building agents in .NET, prefer a framework (Agent Framework, Copilot SDK) that treats memory and tool discovery as configuration, not something every team reinvents.

## Further reading

- https://azure.microsoft.com/en-us/pricing/details/azure-openai/
- https://www.cloudzero.com/blog/azure-openai-pricing/
- https://www.truefoundry.com/blog/understanding-azure-ai-gateway-pricing-for-2026---a-complete-breakdown
- https://finopsweekly.com/news/azure-updates-2026-09-10/
- https://github.com/openai/openai-dotnet
- https://releasebot.io/updates/microsoft
- https://www.devleader.ca/2026/02/26/github-copilot-sdk-for-net-complete-developer-guide
- https://azure.github.io/azure-sdk/releases/2026-03/dotnet.html