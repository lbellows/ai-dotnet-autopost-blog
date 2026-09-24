---
layout: post
title: "One Interface, Many Models: The Case for Microsoft.Extensions.AI in .NET"
date: 2026-09-24 12:37:28 -0400
tags: [openai, access, azure, deployment, grok-4-6, claude-sonnet-5]
author: the.serf
---

Model providers are multiplying faster than NuGet can version them. In the space of a few months, Microsoft Foundry alone has added GPT-6 Astra through a Limited Access Program, Claude Fable 5 and Claude Opus 4.8 as generally available options, and Microsoft's own in-house MAI-Image-2.5 and MAI-Code-1-Flash models running production workloads inside Bing, PowerPoint, and GitHub Copilot. If your .NET codebase talks directly to `AzureOpenAIClient` or a vendor SDK with hardcoded model names, every one of those launches is a mini-migration. That's the exact problem `Microsoft.Extensions.AI` was built to absorb.

## The abstraction, in plain terms

`Microsoft.Extensions.AI.Abstractions` gives you a single `IChatClient` interface that sits between your application code and whichever model backend actually answers the prompt. Reference implementations exist for Azure OpenAI, OpenAI directly, and Ollama for local models, and the abstraction is designed so vendor-specific SDKs plug in behind it rather than leaking into your business logic. Middleware in the surrounding `Microsoft.Extensions.AI` package adds caching, telemetry, and tool-calling as decorators around that interface, so you get observability and resilience without touching call sites.

Visual Studio 2026's own provider story reinforces why this matters: its release notes list Microsoft Foundry, OpenAI, Anthropic, and Ollama as supported chat providers, plus custom endpoints for OpenAI and Ollama — meaning even the IDE's own agent tooling assumes you'll swap providers, not marry one.

## Why this earns its keep now

Three concrete pressures are why "just call the Azure OpenAI SDK directly" is starting to look short-sighted:

- **Provider churn.** Frontier models are arriving and being deprecated on a cadence measured in weeks. GPT-6 Astra rolled out through a Limited Access Program with explicit "availability expanding... over the coming days" language — the kind of staged rollout that makes hardcoded client construction brittle.
- **Cost and latency tradeoffs are provider-specific.** Microsoft's own MAI-Image-2.5, for example, is reported to cut GPU costs materially versus GPT-Image-2 while improving P95 latency in production image-editing workloads. If switching providers can mean a real efficiency win, your architecture should make that switch a config change, not a rewrite.
- **Local and cloud need to coexist.** Ollama support in the same abstraction means you can develop and test against a local model and ship against Azure-hosted Foundry models without maintaining two codepaths.

## What it looks like in code

Here's a minimal but complete setup: register a chat client behind the abstraction, wire in caching, and handle the case where the configured deployment doesn't exist — a mistake every team makes at least once when a model gets renamed or retired mid-project.

```csharp
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton(new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
    new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")!)));

services.AddChatClient(sp =>
        sp.GetRequiredService<AzureOpenAIClient>()
          .GetChatClient("gpt-6-astra") // deployment name, not a model literal
          .AsIChatClient())
    .UseFunctionInvocation()
    .UseDistributedCache(); // requires a registered IMemoryCache/IDistributedCache

services.AddMemoryCache();

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatClient>();

try
{
    var response = await client.GetResponseAsync("Summarize this quarter's Azure spend.");
    Console.WriteLine(response.Text);
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    // Deployment renamed, retired, or not yet rolled out to this region —
    // the exact failure mode you'll hit swapping in a Limited Access model.
    Console.Error.WriteLine($"Deployment not found: {ex.Message}");
}
```

Swap the `AzureOpenAIClient` registration for an Ollama-backed client or a different Foundry deployment, and everything downstream — the caching decorator, the function-invocation middleware, the call site — stays untouched. That's the entire pitch: the provider becomes a DI registration, not an architectural commitment.

## The tradeoff worth naming

Abstraction always costs something. You lose access to provider-specific parameters until the abstraction catches up to them, and debugging occasionally means peeling back a layer to see which underlying SDK call actually fired. For teams standardized on one provider indefinitely, this is arguably over-engineering. But if your organization is already juggling Foundry, direct OpenAI access, and local Ollama for development — which, per Visual Studio 2026's own provider list, is now the default assumption — the abstraction pays for itself the first time a model gets renamed out from under you.

![One Interface, Many Models: The Case for Microsoft.Extensions.AI in .NET meme](https://i.imgflip.com/b1xhnq.jpg)

## Practical takeaways

- Put `IChatClient` at your application boundary now, even if you only use one provider today — the cost of adding the interface later is a full refactor.
- Use deployment names, not model literals, in configuration, and expect 404s as a normal failure mode when a Limited Access rollout hasn't reached your region yet.
- Layer caching and function-invocation as `IChatClient` middleware instead of hand-rolling retry and cache logic per provider.
- Treat Ollama support as your local dev and CI story, not just a demo footnote — it keeps integration tests off the metered API entirely.

## Further reading

- https://www.infoq.com/news/2024/10/dotnet-ai-integration-libraries/
- https://www.infoq.com/news/2024/09/azure-ai-sdk-dotnet/
- https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes
- https://azure.microsoft.com/en-us/blog/product/microsoft-foundry/
- https://azure.microsoft.com/en-us/blog/introducing-anthropics-claude-models-in-microsoft-foundry-bringing-frontier-intelligence-to-azure/
- https://venturebeat.com/infrastructure/microsoft-launches-new-in-house-ai-models-it-says-cut-costs-up-to-89-versus-openai
- https://venturebeat.com/technology/openai-releases-gpt-6-sol-and-luna-models-slashing-api-costs-50-or-more