---
layout: post
title: "When Your Model Upgrades Itself Out From Under You"
date: 2026-09-20 11:34:42 -0400
tags: [model, canary, azureopenai, foundry, grok-4-6, claude-sonnet-5]
author: the.serf
---

Every few weeks a shinier frontier model shows up in the Microsoft Foundry catalog, and it's tempting to just flip the deployment name and move on. Anthropic's Claude Fable 5, OpenAI's GPT-6 Astra, updated Opus builds — the catalog keeps growing, and Foundry makes swapping model generations look like changing a config value. It isn't. Model retirements come with lifecycle deadlines, and a name swap can silently change response formatting, tool-call schemas, and even how the model interprets your system prompt. If your only test is "did it compile," you will find out about the breakage in production, from a customer, at the worst possible time.

## The upgrade is not a drop-in replacement

Microsoft's own guidance on moving between model generations in Foundry lays out a multi-phase migration process rather than a single flag flip — evaluate, stage, canary, monitor, then promote. That's a deliberate signal: model generations are not wire-compatible contracts. A newer model might:

- Return JSON with slightly different key ordering or optional fields your deserializer doesn't expect.
- Change how it invokes function/tool calls — arguments that used to arrive as strings might arrive typed, or vice versa.
- Alter refusal or safety behavior in ways that change downstream branching logic.
- Shift latency and token-usage profiles enough to blow past a timeout or a budget alert you tuned for the old model.

None of this shows up in a quick manual smoke test where you paste a prompt into a playground and eyeball the answer. It shows up in the 2% of production traffic that hits an edge case your offline eval set never covered.

## Pin deployments, don't pin trust

The practical fix is boring but effective: treat the model deployment as a versioned dependency, not a string literal buried in a call site. Keep the old and new deployments live side by side, route a small percentage of traffic to the new one, and compare structured outputs — not just "did it answer" but "did it answer in the shape my code expects."

```csharp
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

public sealed class ModelCanaryRouter
{
    private readonly AzureOpenAIClient _client;
    private readonly string _stableDeployment;
    private readonly string _canaryDeployment;
    private readonly double _canaryTrafficPercent;

    public ModelCanaryRouter(IConfiguration config)
    {
        _client = new AzureOpenAIClient(
            new Uri(config["AzureOpenAI:Endpoint"]!),
            new DefaultAzureCredential());

        _stableDeployment = config["AzureOpenAI:StableDeployment"]!;   // e.g. "gpt-5-1-prod"
        _canaryDeployment = config["AzureOpenAI:CanaryDeployment"]!;   // e.g. "gpt-6-astra-canary"
        _canaryTrafficPercent = double.Parse(config["AzureOpenAI:CanaryPercent"] ?? "0");
    }

    public async Task<ChatCompletion> CompleteAsync(IEnumerable<ChatMessage> messages)
    {
        var useCanary = Random.Shared.NextDouble() * 100 < _canaryTrafficPercent;
        var deployment = useCanary ? _canaryDeployment : _stableDeployment;
        var chatClient = _client.GetChatClient(deployment);

        try
        {
            var response = await chatClient.CompleteChatAsync(messages);
            LogModelResponse(deployment, response.Value);   // schema + latency comparison, not just success/fail
            return response.Value;
        }
        catch (RequestFailedException ex) when (useCanary)
        {
            // Canary failed — fall back to stable so users never see the blast radius.
            var fallback = _client.GetChatClient(_stableDeployment);
            LogCanaryFailure(deployment, ex);
            return (await fallback.CompleteChatAsync(messages)).Value;
        }
    }
}
```

The point isn't the exact percentage logic — it's that the fallback path exists at all, and that `LogModelResponse` is doing schema-shape comparison, not just "200 OK." If the canary's tool-call arguments deserialize into a different shape than the stable model's, you want to know that from telemetry, not from a support ticket.

![When Your Model Upgrades Itself Out From Under You meme](https://i.imgflip.com/b1lfjo.jpg)

## Practical takeaways

- **Treat model generation as a breaking-change boundary**, on par with a major NuGet version bump. Run your existing eval set against the new model before it's the only option — Foundry's retirement dates aren't a suggestion, they're a countdown.
- **Diff structured output, not prose.** If your app parses JSON, function calls, or citations out of a response, add assertions on shape and types, not just "non-null."
- **Keep a fallback deployment warm.** A canary that fails open to nothing is worse than no canary at all.
- **Watch token and latency profiles per model**, not just per endpoint — a "better" model that's 3x slower can quietly violate SLAs you forgot you had.
- **Coordinate with whoever owns cost dashboards.** A new model generation frequently ships with a different price-per-token, and canary traffic at 5% can still move a bill if the new model is verbose.

None of this requires exotic tooling — it's the same discipline .NET teams already apply to package upgrades, just pointed at a dependency that happens to think for a living.

## Further reading

- https://azure.microsoft.com/en-us/blog/tag/ai/
- https://azure.microsoft.com/en-us/blog/product/azure-ai/
- https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes
- https://azurecharts.com/updates?service=147&search=1
- https://news.baeke.info/