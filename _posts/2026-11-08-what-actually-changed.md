---
layout: post
title: "GPT-6.1 Terra's Larger Context Window Arrives as Foundry's November Retirements Begin"
date: 2026-11-08 12:43:17 -0500
tags: [azure, november, haiku, claude, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

Per the changelog write-ups circulating in early November, Terra is billed as "built for long-running, multi-step work," with a context window of roughly 1.15M tokens — up from 500K — a maximum output of 512K tokens, and an added Ultra effort level that sits above Max. If you have been sizing retrieval pipelines around a 500K ceiling, that ceiling just moved, and so did the cost model attached to it.

The pricing picture is where things get messy, and worth being careful about. Aggregator snapshots as of November 7, 2026 place Claude Haiku 5.5 and GPT-6 Luna in the same $0.10 / $0.50 per-million-token band, and note that OpenAI's direct API is cheaper than marketplace listings for the GPT-6.1 Sol and Terra models. One comparison describes the move from GPT-5.6 Sol to GPT-6.1 Sol as roughly an 80% cut in direct price. Treat those as directional rather than contractual — pricing pages move faster than blog posts about pricing pages.

## The Azure side is where the real work is

If you deploy through Azure rather than the direct OpenAI API, the billing mechanics deserve a closer look. Microsoft Learn documents that standard pay-as-you-go GPT-6 deployments use **separate short-context and long-context pricing categories**. The number of *input* tokens determines which category you land in — output tokens do not. And an API parameter like `max_output_tokens` does not reserve context budget when less is available. That last detail is the one that bites: teams assume the parameter caps their exposure, and it does not cap the input-side classification that drives the rate.

The thresholds and rates themselves live on the Azure OpenAI pricing page rather than in the model concepts doc, so read them there before you size a workload.

Meanwhile, LiteLLM listed 123 Azure AI Foundry models as of November 5, 2026, with GPT-6.1 Terra, Claude Opus 5.5 Fast, and Cohere Embed V5 Fast as the most recently added names. That is a routing surface, not a model launch, but it tells you Terra is reachable through the same gateway plumbing you already have.

## Retirement dates are the actual deadline

The more urgent engineering work is not adoption — it is decommissioning. Microsoft Foundry retires codex-mini and Claude Haiku 4.5 on November 15, two `gpt-chat-latest` previews on November 17, and o1, o1-pro, and five more o-series models on November 19. The stated replacements are `gpt-5.6-sol` for the general case, with `gpt-5.6-terra` named for o3-mini and o4-mini. Note the asymmetry: Anthropic's own Claude API still lists Haiku 4.5 and Opus 4.5 as active with "at least 60 days' notice," while Foundry's schedule puts Haiku 4.5 at November 15 and Opus 4.5 at November 24. When two vendors disagree about a model's lifecycle, your integration tests should follow the more aggressive date.

Retired-model inference returns `410 Gone`, which is at least an honest failure mode rather than a silent behavior change.

```csharp
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

// Foundry endpoint + key; swap for DefaultAzureCredential in production.
var client = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")!),
    new AzureKeyCredential(Environment.GetEnvironmentVariable("FOUNDRY_API_KEY")!));

ChatClient chat = client.GetChatClient("gpt-5.6-sol"); // replacement for retiring o-series

var options = new ChatCompletionOptions
{
    MaxOutputTokens = 4096 // does NOT reserve context budget on pay-as-you-go GPT-6
};

try
{
    ChatCompletion completion = await chat.CompleteChatAsync(
        [new UserChatMessage("Summarize this 900K-token incident log.")],
        options);

    Console.WriteLine(completion.Content[0].Text);
}
catch (RequestFailedException ex) when (ex.Status == 410)
{
    // Retired model, or a model the deployment no longer serves.
    Console.Error.WriteLine($"Model unavailable: {ex.ErrorCode}. Check the Foundry retirement calendar.");
}
catch (RequestFailedException ex) when (ex.Status == 400)
{
    // Common on long-context requests: input crossed the long-context pricing
    // category, or the request exceeded the deployment's context window.
    Console.Error.WriteLine($"Request rejected: {ex.Message}");
}
```

The point of that sample is the two catch blocks, not the happy path. On a model family where input size silently reclassifies your billing category, a 400 is frequently a cost event rather than a syntax error, and a 410 means you missed a calendar entry.

## Practical takeaways

- **Do not assume the context window is free to use.** Long-context pricing categories mean the marginal cost of stuffing tokens is non-linear, and `max_output_tokens` is not a budget guard.
- **Pin your model versions explicitly.** With o-series, codex-mini, and Claude Haiku 4.5 all retiring inside a two-week span, floating aliases are a production risk.
- **Test the 410 path.** Retired-model inference returns a distinct status; if your error handling collapses that into a generic retry, you will retry into a wall.
- **Watch the vendor disagreement.** Foundry's retirement dates and Anthropic's own lifecycle notices do not match for Haiku 4.5 and Opus 4.5. Prefer the earlier date.
- **Check the promotion window.** Azure OpenAI metered pricing for GPT-5.6 Sol is reduced to $4.00 per 1M input and $20.00 per 1M output (20% off input, 33.3% off output) through at least November 30, 2026 — so a cost comparison done today may not hold in December.

## What to do before the calendar catches up

The migration itself is usually mechanical: swap the deployment name, re-run your evaluation suite, confirm the output shape. The hard part is organizational — knowing which of your services still call a model that dies on the 15th, the 17th, or the 19th. Inventory your deployment names against the retirement list, then make the 410 path observable in your telemetry so the next retirement is a dashboard event instead of a page.

Microsoft Ignite runs November 17–20, 2026, which lands squarely in the middle of that retirement stretch. Expect the calendar and the conference to talk past each other; plan for the dates, not the keynote.

## Further reading

- https://www.promptzone.com/ai-model-releases
- https://www.24bitsystem.com/latest-ai-models
- https://enjoy.dev/blog/ai-models-shutting-down-november-2026/
- https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure
- https://www.clone-systems.com/blog/new-ai-models-november-2026-release-guide-and-security
- https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026
- https://autom8labs.io/blog/ai-insight-november-2026
- https://platform.claude.com/docs/en/release-notes/overview
- https://azure.microsoft.com/en-us/blog/gpt-5-6-now-available-in-microsoft-foundry/
- https://models.litellm.ai/providers/azure_ai
- https://azure.github.io/azure-sdk/releases/latest/dotnet.html
- https://github.com/Azure/azure-sdk-for-net/releases