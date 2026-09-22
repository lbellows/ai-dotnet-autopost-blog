---
layout: post
title: "Your Foundry Agent Doesn't Know When to Stop — Your Code Has to Tell It"
date: 2026-09-22 12:24:03 -0400
tags: [agent, foundry, circuit, tpm, grok-4-6, claude-sonnet-5]
author: the.serf
---

Every Azure AI Foundry model deployment ships with a configurable tokens-per-minute cap, and it's tempting to treat that setting as a billing dial you touch once and forget. It isn't. It's the only thing standing between a slightly-too-eager agent loop and a token bill that looks like a typo. If you're building agentic workflows on Foundry — chaining tool calls, letting a model decide when it's "done" — the TPM cap is your circuit breaker of last resort, and it only helps if the rest of your code is built to respect it rather than fight it.

## Why this bites agent builders specifically

A single chat completion has a natural stopping point: the model answers, you render it, done. An agent loop doesn't have that guardrail built in. It calls a tool, evaluates the result, decides whether to call another tool, and repeats — and if the termination condition is even slightly wrong (a tool that never returns the expected shape, a retry policy with no ceiling, a model that gets stuck re-trying the same failed action), that loop will happily run until something external stops it. On a pay-as-you-go Foundry deployment, "something external" is either your wallet or the TPM cap throwing a 429 back at you. The TPM cap is the cheaper failure mode. The question is whether your code treats a 429 as a signal to back off gracefully, or as just another exception to retry into oblivion.

This isn't a hypothetical. Azure's own Foundry pricing guidance calls out this exact scenario: no cap, plus a bad retry loop or a non-terminating tool-call cycle, and you can burn a monthly token budget in hours, not weeks. The fix isn't "raise the cap" — it's building the loop so that hitting the cap is a controlled pause, not a wall you slam into repeatedly while your retry logic keeps swinging.

## What actually changed under the hood

If you're using the Azure AI Projects SDK to talk to Foundry Agent Service, note that the tracing provider name changed from `azure.ai.agents` to `microsoft.foundry` in a recent SDK update — worth knowing if you have dashboards or log queries filtering on the old provider string, because they'll quietly go dark rather than error out. The same update folded in evaluations, red-teaming, and schedule support directly against a Foundry project, which is useful if you want to budget-test an agent's tool-call behavior before it's anywhere near production traffic.

## A circuit breaker that actually respects the cap

The pattern below isn't exotic — it's a bounded retry with an explicit circuit breaker around a Foundry-hosted chat/agent call using `Azure.AI.OpenAI`. The point is the failure path: catching the rate-limit response, honoring `Retry-After`, and — critically — giving up after a fixed number of trips instead of retrying forever.

```csharp
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

public sealed class BudgetGuardedAgentStep
{
    private readonly ChatClient _chatClient;
    private int _consecutiveRateLimitHits = 0;
    private const int MaxTrips = 3;

    public BudgetGuardedAgentStep(AzureOpenAIClient client, string deploymentName)
    {
        _chatClient = client.GetChatClient(deploymentName);
    }

    public async Task<ChatCompletion?> StepAsync(IEnumerable<ChatMessage> messages)
    {
        try
        {
            ClientResult<ChatCompletion> result = await _chatClient.CompleteChatAsync(messages);
            _consecutiveRateLimitHits = 0; // reset on success
            return result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            _consecutiveRateLimitHits++;

            if (_consecutiveRateLimitHits >= MaxTrips)
            {
                // Circuit is open: stop the loop, don't burn the TPM budget further.
                throw new InvalidOperationException(
                    "Agent step tripped the TPM cap repeatedly. Halting loop for manual review.", ex);
            }

            TimeSpan delay = ex.GetRawResponse()?.Headers.TryGetValue("Retry-After", out var raw) == true
                && int.TryParse(raw, out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : TimeSpan.FromSeconds(Math.Pow(2, _consecutiveRateLimitHits));

            await Task.Delay(delay);
            return null; // caller decides whether to re-invoke or abandon this iteration
        }
    }
}
```

The important lines aren't the happy path — they're the `MaxTrips` ceiling and the fact that a `null` return forces the caller to make an explicit decision about whether the loop continues. No silent infinite retries, no "just try again forever," no agent that keeps hammering a capped deployment until someone notices the spend dashboard.

![Your Foundry Agent Doesn't Know When to Stop — Your Code Has to Tell It meme](https://i.imgflip.com/b1r7jm.jpg)

## Practical takeaways

- Set the TPM cap deliberately on every Foundry deployment an agent touches — treat it as a safety rail, not a performance knob.
- Wrap tool-call loops with a bounded retry and an explicit circuit breaker; "keep trying" is not a termination condition.
- If you're on Azure.AI.Projects and filtering telemetry by provider name, update queries from `azure.ai.agents` to `microsoft.foundry` — the old filter will just quietly stop matching.
- Before shipping an agent that chains tool calls autonomously, run it against a capped test deployment first. Watching it hit a 429 in staging is a lot cheaper than watching it hit one in production billing.

None of this requires exotic tooling — it requires resisting the urge to assume the model will know when to stop, because it won't. Your retry policy is the adult in the room.

## Further reading

- https://www.wrvishnu.com/azure-ai-foundry-pricing-2026/
- https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/ai/Azure.AI.Projects/CHANGELOG.md
- https://azure.microsoft.com/en-us/pricing/details/azure-openai/
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-build-2026/
- https://github.com/nicholasdbrady/foundry-docs/issues/956