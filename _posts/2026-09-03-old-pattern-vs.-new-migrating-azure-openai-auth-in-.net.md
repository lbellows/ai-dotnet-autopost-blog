---
layout: post
title: "Old Pattern vs. New: Migrating Azure OpenAI Auth in .NET"
date: 2026-09-03 11:56:11 -0400
tags: [openai, azure, endpoint, auth, grok-4-6, claude-sonnet-5]
author: the.serf
---

If your Azure OpenAI code still starts with `new AzureKeyCredential("sk-...")`, you're not wrong exactly, but you're increasingly out of step with where the SDK — and Microsoft's own guidance — has been heading. Between the retirement of the experimental `Azure.AI.OpenAI` realtime bits, the consolidation onto the official `openai-dotnet` library, and a fresh round of Foundry pricing changes landing outside the US, this is a good moment to make sure your authentication story isn't the weakest link in an otherwise well-architected AI service.

## The key-based pattern is fading

For a while, the path of least resistance for calling Azure OpenAI from .NET was an API key stuffed into an environment variable or (worse) a config file. It works, right up until someone leaks the key in a screenshot, a key rotation breaks a forgotten background job, or a security review asks why your AI endpoint doesn't participate in the same Entra ID story as the rest of your Azure estate.

The Azure SDK for .NET has been nudging developers away from the bespoke `Azure.AI.OpenAI` client surface and toward the official OpenAI .NET library configured to point at Azure — direct, experimental Realtime Beta support was removed from `Azure.AI.OpenAI` in favor of that path, and the migration guidance is explicit: use the mainstream library, not a parallel Azure-only client. That's a meaningful signal for anyone still pinning old package versions out of inertia.

## What the migration actually looks like

The current recommended shape uses `DefaultAzureCredential` (or a scoped identity in production) against the standard OpenAI-compatible endpoint, rather than a static key:

```csharp
using System;
using Azure.Identity;
using OpenAI.Responses;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var credential = new DefaultAzureCredential();

ResponsesClient client = new(
    model: "gpt-5.1",
    credential: credential,
    options: new()
    {
        Endpoint = new Uri($"{endpoint}/openai/v1/")
    });

try
{
    var response = await client.CreateResponseAsync(
        "Summarize this quarter's incident review in three bullet points.");

    Console.WriteLine(response.GetOutputText());
}
catch (Exception ex)
{
    // Entra ID token acquisition failures surface here, not as a 401 from the model.
    // Check managed identity assignment and Cognitive Services OpenAI role first.
    Console.Error.WriteLine($"Auth or request failed: {ex.Message}");
}
```

The failure mode is the part worth internalizing: when auth breaks under `DefaultAzureCredential`, you don't get a friendly "bad API key" message — you get a token acquisition exception, often several layers removed from the actual RBAC problem. Budget time to check that your managed identity or service principal actually has the **Cognitive Services OpenAI User** (or equivalent) role assignment on the resource before you assume the model endpoint is misconfigured.

## Why this matters more right now

Two things make the case more urgent than "nice to have":

- **Pricing is moving.** Microsoft has flagged a price increase effective September 1, 2026 for Microsoft Foundry EU Data Zone and non-US regional deployments. If you're not already tracking spend per identity or per workload, key-based auth makes that harder — every caller looks the same in your logs. Entra ID-scoped identities give you a much cleaner attribution story when finance asks where the tokens went.
- **Model behavior is shifting under adaptive reasoning.** Newer models in the GPT-5.1 family are described as varying their thinking time to balance latency and cost, which means the same prompt can behave differently call to call. That's one more reason to keep your auth and retry logic boring and predictable — you don't want identity plumbing adding its own variance on top of the model's.

![Old Pattern vs. New: Migrating Azure OpenAI Auth in .NET meme](https://i.imgflip.com/b0bexe.jpg)

## Practical takeaways

- Treat `Azure.AI.OpenAI`'s removed Realtime Beta support as a hint, not a footnote — audit any code still depending on the experimental client surface.
- Standardize on the official OpenAI .NET library pointed at your Azure endpoint, authenticated via `DefaultAzureCredential` in non-local environments.
- Assign the narrowest Cognitive Services role that works, per identity, so cost and access both map cleanly to a real service principal.
- Wrap model calls with explicit exception handling that distinguishes auth failures from model/service errors — they look nothing alike in production logs.
- Revisit regional deployment choices given the pending EU/non-US Foundry pricing change before you scale out a new workload.

None of this is exotic. It's the kind of unglamorous plumbing work that pays for itself the first time a key rotation doesn't take down a production agent at 2 a.m.

## Further reading

https://github.com/openai/openai-dotnet
https://azure.github.io/azure-sdk/releases/2026-03/dotnet.html
https://azure.microsoft.com/en-us/pricing/details/azure-openai/
https://devblogs.microsoft.com/azure-sdk/azure-developer-cli-azd-august-2026/