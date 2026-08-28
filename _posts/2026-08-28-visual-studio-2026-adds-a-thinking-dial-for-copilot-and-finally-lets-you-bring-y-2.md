---
layout: post
title: "Visual Studio 2026 Adds a Thinking Dial for Copilot — and Finally Lets You Bring Your Own Model"
date: 2026-08-28 11:01:57 -0400
tags: [agent, copilot, byok, ide, dial, claude-sonnet-5]
author: the.serf
---

Microsoft quietly shipped two changes to Visual Studio 2026 on August 25 that matter more than their release-note bullet points suggest: a "thinking effort" slider for Copilot inside the stable channel, and Bring Your Own Key (BYOK) support for the new Agent (Preview) mode in Insiders. Together they turn Copilot from a fixed-cost black box into something engineers can actually tune — for latency, for spend, and for which vendor's model sits behind the keyboard shortcut.

## The thinking-effort dial

Visual Studio 18.9 lets you choose **low, medium, or high** thinking effort for supported Copilot models, trading response quality against token usage. This is the IDE equivalent of the reasoning-effort parameter you've probably already wired into API calls against reasoning models — except now it's a first-class UI control instead of a payload field you have to remember to set.

For day-to-day .NET work, this is the practical takeaway: stop treating every Copilot query as if it deserves frontier-model deliberation. Renaming a variable or fixing a null-check doesn't need "high" effort; refactoring an async state machine across a legacy service probably does.

![Visual Studio 2026 Adds a Thinking Dial for Copilot — and Finally Lets You Br...](https://i.imgflip.com/azvb06.jpg)

## BYOK arrives for Agent Mode

The more structurally interesting change is in Visual Studio 2026 Insiders: the new **Agent (Preview)**, built on the GitHub Copilot SDK-powered harness, now supports Bring Your Own Key by default across Community, Professional, and Enterprise. You can point it at:

- **Microsoft Foundry**
- **OpenAI**
- **Anthropic**
- **Ollama** (for local models)

Custom endpoint support for OpenAI and Ollama is marked "coming soon," and — notably — BYOK from the earlier Ask/Agent modes is *not* carried forward, so if you had a key wired in previously, budget time to reconnect it under the new harness. Agent (Preview) also gained the ability to connect Git provider MCP servers (GitHub and Azure DevOps), which gives Copilot richer pull-request context without you hand-rolling a tool integration.

For teams already standardizing on Azure AI Foundry for governance, this closes a real gap: your IDE agent and your production inference layer can now point at the same deployed model, with the same quota and the same Entra-backed identity, instead of Copilot quietly routing through a GitHub-hosted default.

```jsonc
// Illustrative shape only — configure via the Agent (Preview) settings UI,
// not by hand-editing a settings file.
{
  "copilot.agent.provider": "microsoftFoundry",
  "copilot.agent.endpoint": "<your-foundry-project-endpoint>",
  "copilot.agent.authMode": "entra"
}
```

![Visual Studio 2026 Adds a Thinking Dial for Copilot — and Finally Lets You Br...](https://i.imgflip.com/azvb06.jpg)

## Why this matters beyond the IDE

Two threads from the broader ecosystem make this timely rather than cosmetic:

- **Microsoft Agent Framework for .NET** shipped a breaking change migrating MCP long-running task support to the 2026-07-28 Tasks extension spec, alongside a bump of the Anthropic SDK dependency and session-persisted chat client routing (including Azure Blob Storage session persistence and hosted-agent state persisted in Foundry). If you're building custom agents rather than just using Copilot, expect to touch your MCP task-handling code on upgrade.
- Cost is not an abstraction anymore. FinOps coverage from late August notes Anthropic's Claude Code cut its workflow tool description from roughly 5.7K tokens to about 1K — a meaningful per-turn prompt-tax reduction for agentic coding sessions. Thinking-effort controls and BYOK are the .NET-tooling side of the same industry-wide push: every extra reasoning token or redundant system prompt is now a line item someone in finance will eventually ask about.

## Practical checklist

- Pin Copilot to **low** effort for boilerplate, **high** for architecture-sensitive refactors — don't leave it on a single default.
- If you manage Foundry deployments, test BYOK in Insiders before it hits stable; preview surfaces can still change API behavior.
- Audit any Agent Framework .NET projects for the MCP Tasks extension migration before your next upgrade — it's marked breaking for a reason.
- Watch for the promised custom-endpoint support for OpenAI and Ollama; that's the piece that will let fully private or on-prem model setups plug into VS without a cloud round-trip.

None of this is a new model or a new benchmark chart. It's Microsoft doing the less glamorous but more durable work of making the IDE's AI layer configurable, auditable, and cost-aware — which, for anyone footing an Azure bill, is arguably the more useful headline.

## Further reading

- https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes
- https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes-insiders
- https://github.com/microsoft/agent-framework/releases
- https://releasebot.io/updates/microsoft
- https://finopsweekly.com/news/ai-economics-provider-updates-2026-08-28/
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-build-2026/
- https://tech.hub.ms/dotnet/roundups/weekly-dotnet-roundup-2026-08-24
- https://github.blog/changelog/2026-06-02-copilot-sdk-is-now-generally-available/