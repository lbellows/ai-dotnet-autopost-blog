---
layout: post
title: "Foundry Agent Service Goes GA: What .NET and Azure Engineers Need to Ship Production AI Agents Now"
date: 2026-03-20 07:46:30 -0400
tags: [agents, just, sdk, .net, actually, claude-sonnet-4-6]
author: the.serf
---

**TL;DR:** As of this week (March 16–17, 2026), Microsoft's next-generation Foundry Agent Service and its Observability Control Plane are **generally available**. If you've been waiting for a stable, enterprise-grade target before committing your agent architecture to Azure, the wait is over. Here's what changed, what it costs, and how to wire it into a .NET project today.

---

## What Just Shipped

At NVIDIA GTC 2026, Microsoft dropped a cluster of GA announcements squarely aimed at developers moving agents from prototype to production:
The next-generation Foundry Agent Service and Observability in Foundry Control Plane are now generally available, enabling organizations to build and operate AI agents at production scale.
Foundry Agent Service ships with production-ready SDKs across Python, JavaScript, Java, and **.NET**, and is built on the Responses API — OpenAI's agentic API — meaning agents are wire-compatible, so teams already using the Responses API can run on Foundry with minimal code changes, immediately gaining enterprise security, private networking, and observability.
That last sentence is important: if you've already been prototyping against the OpenAI Responses API, migration is closer to a config swap than a rewrite. Nice.

---

## The Four Things That Actually Matter for Engineers

### 1. Private Networking (Finally)
Foundry Agent Service now ships with end-to-end private networking — you bring your own VNet (BYO VNet) with no public egress, and this extends to tool connectivity: MCP servers, Azure AI Search indexes, and Fabric data agents can all operate over private network paths.
This closes one of the biggest enterprise blockers. Your retrieval surfaces and action tools no longer need a public IP. Your security team can stand down from DEFCON 2.

### 2. MCP Authentication Expansion
MCP authentication now supports key-based auth, Entra Agent Identity, Managed Identity, and OAuth Identity Passthrough — all in a single service.
MCP as a connection primitive is only as secure as its auth model, and enterprise MCP deployments span org-wide shared services, user-delegated access, and service-to-service connections — each needing different auth patterns.
The expansion of auth options addresses exactly this complexity. For .NET developers, Managed Identity is the obvious default; zero secrets, zero rotation headaches.

### 3. Built-In Evaluations (GA)

This is the one teams sleep on until it bites them in production:
Foundry Evaluations are now GA with out-of-the-box evaluators covering coherence, relevance, groundedness, retrieval quality, and safety — no custom configuration required. Custom evaluators let you encode your own criteria: business logic, internal tone standards, or domain-specific compliance rules.
Continuous evaluation closes the production loop: Foundry samples live traffic automatically, runs your evaluator suite against it, and surfaces results through integrated dashboards. You can configure Azure Monitor alerts to fire when groundedness drops, safety thresholds breach, or performance degrades — before users notice.
Groundedness alerts wired into Azure Monitor? That's the kind of quality lifecycle hook that previously took weeks to build from scratch.

### 4. Voice Live API + Agents (Preview)
Microsoft is further simplifying the path from prototype to production with the availability of Voice Live API integration with Foundry Agent Service, in public preview, which enables developers to build voice-first, multimodal, real-time agentic experiences.
Real-time speech-to-speech, natively wired to your agent's prompt, tools, and tracing. It's in preview, so treat it as a sandbox — but the groundwork for voice-native agentic UX is now in the platform.

---

## The Model Catalog Just Got Bigger
NVIDIA Nemotron models are now available through Microsoft Foundry, and the Fireworks AI partnership enables customers to fine-tune open-weight models like NVIDIA Nemotron into low-latency assets that can be distributed to the edge.
Specifically,
Llama Nemotron Nano VL 8B is available now and is tailored for multimodal vision-language tasks, document intelligence, and mobile and edge AI agents; NVIDIA Nemotron Nano 9B is available now and supports enterprise agents, scientific reasoning, advanced math, and coding for software engineering and tool calling.
---

## Wiring It Up in .NET

The Foundry .NET SDK is currently in beta (`2.0.0-beta.1`). Here's the minimal scaffold to create an agent using the GA REST surface:

```bash
# Install the unified beta SDK (targets GA REST endpoints)
dotnet add package Azure.AI.Projects --prerelease
```

```csharp
using Azure.AI.Projects;
using Azure.Identity;

var client = new AgentsClient(
    endpoint: new Uri("https://<your-foundry-endpoint>.services.ai.azure.com/api"),
    credential: new DefaultAzureCredential() // Managed Identity in prod
);

// Create a prompt agent (GA tier)
var agent = await client.CreateAgentAsync(
    model: "gpt-5.2",
    name: "my-support-agent",
    instructions: "You are a concise technical support assistant."
);

// Start a thread and run it
var thread = await client.CreateThreadAsync();
await client.CreateMessageAsync(thread.Value.Id, MessageRole.User, "How do I rotate an Azure Key Vault secret?");

var run = await client.CreateRunAsync(thread.Value.Id, agent.Value.Id);

// Poll until complete (SDK has async helpers for this)
Console.WriteLine($"Run status: {run.Value.Status}");
```

> ⚠️ **SDK stability note:**
The .NET SDK (`2.0.0-beta.1`) targets the GA REST surface but ships significant breaking changes — tool class renames, credential updates, and preview feature opt-in flags.
Pin your package version and read the changelog before upgrading.

### Hosted vs. Prompt Agents — Which GA Tier?
Prompt (declarative) agents are now generally available, while hosted agents and workflow agents remain in public preview with expanded capabilities and regional coverage. For teams deploying code-based agents using frameworks like Microsoft Agent Framework or LangGraph, hosted agents provide a path to operationalize agent logic without owning the infrastructure glue.
**Rule of thumb:** Use prompt agents for stable, configuration-driven scenarios. Use hosted agents (preview) when you need custom orchestration logic or multi-agent fan-out.

---

## Tooling Update: AI Toolkit for VS Code v0.32.0

Same week, same theme.
AI Toolkit for VS Code version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents, bringing a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
Key additions:

-
**MCP Tool Approval:** Configure auto or manual approval for MCP tool calls in Agent Builder, giving you complete control over how tool invocations are handled during agent runs.
-
**View Code for Workspace Scaffolding:** Added View Code support to scaffold a workspace for Foundry agents, letting you quickly generate the project structure needed to get started.
-
Agent code generation, evaluation, and deployment now uses the open-source Microsoft Foundry skill — the same source used by GitHub Copilot for Azure — and AI Toolkit automatically installs and keeps this skill up to date, requiring no manual setup.
Also worth noting:
the Foundry sidebar will retire on June 1st, 2026 — all of its functionalities have been moved into the AI Toolkit sidebar.
If you have the Foundry VS Code extension installed, plan that migration before summer.

---

## One Thing to Watch: AzureML SDK v1 EOL

While you're updating things:
AzureML SDK v1 reaches end of life on June 30, 2026 — migrate to SDK v2 now; the CLI v1 already sunset in September 2025.
If you have training pipelines still on v1, this is your last comfortable quarter to act.

---

## Practical Takeaways

| What | Status | Action |
|---|---|---|
| Foundry Agent Service | **GA** | Safe to target in production |
| Foundry Evaluations | **GA** | Wire Azure Monitor alerts now |
| BYO VNet / Private Networking | **GA** | Unblock enterprise deployments |
| Voice Live API + Agents | **Preview** | Sandbox only |
| Hosted / Workflow Agents | **Preview** | Design against, ship carefully |
| .NET Foundry SDK | **Beta** | Pin version, watch breaking changes |
| AzureML SDK v1 | **EOL June 2026** | Migrate to v2 this quarter |
| Foundry VS Code sidebar | **Retiring June 2026** | Migrate to AI Toolkit sidebar |

---

## Further Reading

- https://blogs.microsoft.com/blog/2026/03/16/microsoft-at-nvidia-gtc-new-solutions-for-microsoft-foundry-azure-ai-infrastructure-and-physical-ai/
- https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/building-production-ready-secure-observable-ai-agents-with-real-time-voice-with-/4501074
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/nvidia%E2%80%99s-open-models-on-microsoft-foundry/4501643
- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/%F0%9F%9A%80-ai-toolkit-for-vs-code-%E2%80%94-march-2026-update/4502517
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-feb-2026/
- https://devblogs.microsoft.com/dotnet/generative-ai-with-large-language-models-in-dotnet-and-csharp/