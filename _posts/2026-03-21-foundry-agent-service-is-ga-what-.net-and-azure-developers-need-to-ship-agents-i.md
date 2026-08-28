---
layout: post
title: "Foundry Agent Service Is GA: What .NET and Azure Developers Need to Ship Agents in Production"
date: 2026-03-21 07:36:14 -0400
tags: [all, .net, agent, agents, azure, claude-sonnet-4-6]
author: the.serf
---

**TL;DR** — Microsoft declared the next-generation Foundry Agent Service generally available this week, dropping alongside the NVIDIA GTC announcements (March 16–21, 2026). You get a Responses-API-compatible runtime, BYO-VNet private networking all the way to your tools, four MCP auth modes, continuous evaluation piped to Azure Monitor, and production-ready .NET SDKs — all in one GA bundle. If you have been waiting for a "real production story" before committing to Foundry agents, the wait is over.

---

## What Just Shipped
Microsoft declared general availability of the next-generation Foundry Agent Service — a modern API, runtime, and developer platform to help teams build agents, move from prototype to production quickly, and operate with confidence.
This wasn't a quiet blog-post drop.
The GA covers expanded Microsoft Foundry capabilities to build, deploy, and operate production-ready AI agents on NVIDIA accelerators and open NVIDIA Nemotron models, new Azure AI infrastructure optimized for inference-heavy, reasoning-based workloads, and deeper integration across Microsoft Foundry, Microsoft Fabric, and NVIDIA Omniverse libraries.
In short: the team picked NVIDIA GTC as the stage and brought the fireworks.

---

## The Runtime: Wire-Compatible, Provider-Agnostic

The most developer-friendly detail is the API surface choice.
Foundry Agent Service is now generally available, with production-ready SDKs across Python, JavaScript, Java, and .NET and a modern API for building durable, tool-using agents. It is built on the Responses API — OpenAI's agentic API — meaning agents are wire-compatible, so teams already using the Responses API can run on Foundry with minimal code changes, immediately gaining enterprise security, private networking, and observability.
The architecture is intentionally pluralistic.
You're not locked to a single model provider or orchestration framework — use a DeepSeek model for planning, an OpenAI model for generation, LangGraph for orchestration, and the runtime handles the consistency layer.
---

## Private Networking All the Way to Your Tools

This is the detail that tends to get glossed over in announcements but matters enormously in regulated industries.
Foundry Agent Service now supports Standard Setup with private networking, where you bring your own virtual network (BYO VNet): no public egress — agent traffic never traverses the public internet. More importantly, private networking is extended to tool connectivity. MCP servers, Azure AI Search indexes, and Fabric data agents can all operate over private network paths — so retrieval and action surfaces sit inside your network boundary, not just inference calls.
That second sentence is the key one. It's one thing to keep model calls private; it's another to keep your *tool calls* private too. For finance, healthcare, and defense workloads, this is table stakes.

### MCP Authentication Expansion
MCP authentication now covers key-based, Entra Agent Identity, Managed Identity, and OAuth Identity Passthrough — all in a single service.
For .NET teams already living inside the Entra ecosystem, Managed Identity means you can wire up an MCP server connection with essentially zero credential management:

```csharp
// Foundry Agent Service with Managed Identity for MCP tool auth
// Uses Azure.Identity — no secrets in config
var credential = new DefaultAzureCredential();
var client = new AgentsClient(
    endpoint: new Uri("https://<your-project>.services.ai.azure.com/api/"),
    credential: credential);

// Agent definition referencing an MCP server over private VNet
var agent = await client.CreateAgentAsync(
    model: "gpt-5.2",
    name: "inventory-agent",
    instructions: "You are an inventory management assistant.",
    tools: [new McpToolDefinition(
        serverLabel: "inventory-mcp",
        serverUrl: "https://mcp.internal.contoso.com/inventory",
        authType: McpAuthType.ManagedIdentity)]);
```

> ⚠️ The .NET SDK (`Azure.AI.Projects` 2.0.0-beta.1 and later) is targeting the GA REST surface. Watch for breaking changes — tool class renames and credential updates shipped in the beta cycle. Pin to the latest beta and follow the migration guide before going live.

---

## Evaluations Are Now a First-Class Production Loop

Forget one-off notebook evaluations.
Foundry Evaluations are now generally available with three layers: out-of-the-box evaluators cover standard RAG and generation scenarios — coherence, relevance, groundedness, retrieval quality, and safety — with no custom configuration required. Custom evaluators let you encode your own criteria: business logic, internal tone standards, domain-specific compliance rules, or any quality signal that doesn't map cleanly to a general evaluator.
The production loop closes with continuous monitoring:
Foundry samples live traffic automatically, runs your evaluator suite against it, and surfaces results through integrated dashboards. You can configure Azure Monitor alerts to fire when groundedness drops, safety thresholds breach, or performance degrades — before users notice.
For .NET teams, this integrates naturally with the Azure Monitor SDKs and Application Insights you already use for the rest of your services. One observability plane to rule them all.

---

## What About Agent Types?

Not everything is fully GA — it's worth knowing the exact tiers:
Prompt (declarative) agents are now generally available, while hosted agents and workflow agents remain in public preview with expanded capabilities and regional coverage. For teams deploying code-based agents using frameworks like Microsoft Agent Framework or LangGraph, hosted agents provide a path to operationalize agent logic without owning the infrastructure glue.
---

## Voice Live: Multimodal Agents in Preview

Bonus round for anyone building conversational experiences:
Microsoft is simplifying the path from prototype to production with the availability of Voice Live API integration with Foundry Agent Service, in public preview, which enables developers to build voice-first, multimodal, real-time agentic experiences.
Real-time speech-to-speech, wired natively to your agent's prompt, tools, and tracing. Worth a spike if your team has been waiting for a managed alternative to rolling your own WebSocket + STT + TTS pipeline.

---

## Developer Tooling: AI Toolkit v0.32.0 Ships the Same Week

The timing is not accidental.
March brought another milestone for AI Toolkit — version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents. This release brings a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
The headline UX change:
the Foundry sidebar has been merged directly into AI Toolkit, allowing you to access the power of both extensions. The AI Toolkit and Foundry extension sidebar panels have been unified into a single My Resources view, with local resources (models, agents, tools) grouped under a Local Resources node and Foundry remote resources appearing right alongside them.
Two other v0.32.0 highlights worth noting:

-
**MCP Tool Approval**: Configure auto or manual approval for MCP tool calls in Agent Builder, giving you complete control over how tool invocations are handled during agent runs.
-
**View Code for Workspace Scaffolding**: Added View Code support to scaffold a workspace for Foundry agents, letting you quickly generate the project structure needed to get started.
> ℹ️ The standalone Foundry sidebar extension will retire on **June 1, 2026**. Consolidate to AI Toolkit now — all functionality is already there.

---

## Practical Takeaways for .NET + Azure Engineers

| Area | Action |
|---|---|
| **SDK** | Upgrade to `Azure.AI.Projects` 2.0.0-beta.1+; target the GA REST surface; expect breaking changes (tool renames, credential updates) |
| **Networking** | Enable BYO VNet in Standard Setup to keep MCP tool calls off the public internet |
| **Auth** | Use `DefaultAzureCredential` + Managed Identity for MCP server connections in Azure-hosted workloads |
| **Evaluation** | Wire the built-in groundedness/safety evaluators to live traffic today; add Azure Monitor alerts before launch |
| **Tooling** | Install AI Toolkit v0.32.0; retire the standalone Foundry extension before June 1 |
| **AzureML** | Migrate off SDK v1 — end of support is **June 30, 2026** |

---

## Further Reading

- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/building-production-ready-secure-observable-ai-agents-with-real-time-voice-with-/4501074
- https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/
- https://blogs.microsoft.com/blog/2026/03/16/microsoft-at-nvidia-gtc-new-solutions-for-microsoft-foundry-azure-ai-infrastructure-and-physical-ai/
- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/%F0%9F%9A%80-ai-toolkit-for-vs-code-%E2%80%94-march-2026-update/4502517
- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/from-prototype-to-production-building-a-hosted-agent-with-ai-toolkit--microsoft-/4501969
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-feb-2026/
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/nvidia%E2%80%99s-open-models-on-microsoft-foundry/4501643