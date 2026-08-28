---
layout: post
title: "Foundry Agent Service Is GA: What .NET and Azure Engineers Need to Ship Production AI Agents Today"
date: 2026-03-18 07:59:43 -0400
tags: [.net, azure, evaluations, just, networking, claude-sonnet-4-6]
author: the.serf
---

**TL;DR** — On March 16, 2026, Microsoft promoted Foundry Agent Service to General Availability at NVIDIA GTC. The GA release brings a Responses-API-compatible runtime, end-to-end private networking (BYO VNet), expanded MCP authentication, production-grade evaluations piped into Azure Monitor, and production-ready SDKs for Python, JavaScript, Java, and **.NET**. If you have been waiting for the "it's really production-ready" signal before committing — that signal just fired.

---

## What Just Shipped
The next-generation Foundry Agent Service and Observability in Foundry Control Plane are now generally available, enabling organizations to build and operate AI agents at production scale. Foundry Agent Service allows teams to quickly develop agents that reason, plan, and act across tools, data, and workflows, while Foundry Control Plane provides end-to-end visibility into agent behavior — unlocking both developer productivity and enterprise trust.
In plain terms: the "hope this holds up in prod" phase is over. Here is the full GA manifest in one place:
- **Foundry Agent Service (GA):** Responses API-based runtime, wire-compatible with OpenAI agents, open model support across Meta, Mistral, DeepSeek, xAI, LangChain, LangGraph, and more.
- **End-to-end private networking:** BYO VNet with no public egress, extended to cover tool connectivity — MCP servers, Azure AI Search, and Fabric data agents.
- **MCP authentication expansion:** Key-based, Entra Agent Identity, Managed Identity, and OAuth Identity Passthrough in a single service.
- **Voice Live (preview) + Foundry Agents:** Real-time speech-to-speech, fully managed, wired natively to your agent's prompt, tools, and tracing.
- **Evaluations (GA):** Out-of-the-box evaluators, custom evaluators, and continuous production monitoring piped into Azure Monitor.
> **Aside:** "Wire-compatible with OpenAI agents" is the diplomatic way of saying you can point existing code at a new endpoint and immediately gain private networking, Entra identity, and compliance controls. Resistance is optional but strongly discouraged.

---

## The .NET SDK Is Here — And It's the One Package You Need
Foundry Agent Service is now generally available with production-ready SDKs across Python, JavaScript, Java, and .NET, and a modern API for building durable, tool-using agents designed to streamline development while scaling reliably for enterprise workloads.
Microsoft has been consolidating its SDK surface for a while.
All Microsoft Foundry SDK development is consolidating into a single `azure-ai-projects` package per language. Agents, inference, evaluations, and memory operations that previously lived in separate packages (such as `azure-ai-agents`) are unified under the `azure-ai-projects` v2 beta line.
For .NET developers, the February wave already shipped `azure-ai-projects` **2.0.0-beta.1**.
March is shaping up to be a big one — SDK GA announcements are on the horizon, and the Foundry SDK will be the single package you need across agents, inference, evaluations, and memory. Get ahead of it now by upgrading to the latest pre-release and targeting the GA REST surface.
Here is what bootstrapping a .NET Foundry agent looks like today:

```bash
dotnet add package Azure.AI.Projects --prerelease
```

```csharp
using Azure.AI.Projects;
using Azure.Identity;

var client = new AgentsClient(
    endpoint: new Uri("https://<your-hub>.services.ai.azure.com/api"),
    credential: new DefaultAzureCredential());

// Create a prompt agent (GA)
AgentCreationOptions options = new("gpt-5.2")
{
    Name        = "support-triage",
    Instructions = "You are a helpful support triage agent. Be concise."
};

Agent agent = await client.CreateAgentAsync(options);
Console.WriteLine($"Agent created: {agent.Id}");
```

> ⚠️ **Note:** Hosted agents and workflow agents remain in public preview.
Prompt (declarative) agents are now generally available, while hosted agents and workflow agents remain in public preview with expanded capabilities and regional coverage.
Plan your GA boundary accordingly.

---

## Private Networking: The Feature Enterprise Teams Actually Need

The single biggest blocker for enterprise adoption of hosted agents has historically been *where agent traffic goes*. That blocker is now addressed.
Foundry Agent Service now supports Standard Setup with private networking, where you bring your own virtual network (BYO VNet): no public egress — agent traffic never traverses the public internet — with container/subnet injection into your network for local communication to Azure resources. More importantly, private networking is extended to tool connectivity: MCP servers, Azure AI Search indexes, and Fabric data agents can all operate over private network paths — so retrieval and action surfaces sit inside your network boundary, not just inference calls.
For regulated industries (finance, healthcare, government), that last sentence is the one worth reading twice. Your RAG retrieval pipeline, your MCP tool calls, your Fabric data agents — all of it can stay off the public internet.

---

## Model Choice: NVIDIA Nemotron Joins the Catalog
NVIDIA Nemotron models are now available through Microsoft Foundry, joining the widest selection of models on any cloud, including the latest reasoning, frontier, and open models. This bolsters a recent partnership bringing Fireworks AI to Microsoft Foundry, enabling customers to fine-tune open-weight models like NVIDIA Nemotron into low-latency assets that can be distributed to the edge.
Concretely,
Llama Nemotron Nano VL 8B is available now and is tailored for multimodal vision-language tasks, document intelligence, and mobile and edge AI agents, while NVIDIA Nemotron Nano 9B is available now and supports enterprise agents, scientific reasoning, advanced math, and coding for software engineering and tool calling.
The architecture is intentionally model-agnostic.
You're not locked to a single model provider or orchestration framework. Use a Llama model for planning, an OpenAI model for generation, LangGraph for orchestration — the runtime handles the consistency layer. Agents, tools, and the surrounding infrastructure all speak the same protocol.
---

## Evaluations GA: Stop Guessing, Start Monitoring
Foundry Evaluations are now generally available with out-of-the-box evaluators covering standard RAG and generation scenarios: coherence, relevance, groundedness, retrieval quality, and safety — no custom configuration required; connect them to a dataset or live traffic and get quantitative scores back. Custom evaluators let you encode your own criteria: business logic, internal tone standards, domain-specific compliance rules, or any quality signal that doesn't map cleanly to a general evaluator.
Continuous evaluation closes the production loop. Foundry samples live traffic automatically, runs your evaluator suite against it, and surfaces results through integrated dashboards. Configure Azure Monitor alerts to fire when groundedness drops, safety thresholds breach, or performance degrades — before users notice.
That last phrase — *before users notice* — is the entire promise of production-grade ML ops, finally baked into the platform rather than bolted on.

---

## Tooling Update: AI Toolkit for VS Code v0.32.0

Hot on the heels of the GA announcement, the AI Toolkit for VS Code also shipped an update this week.
Version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents, bringing a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
Key additions for the day-to-day build loop:

-
**MCP Tool Approval:** Configure auto or manual approval for MCP tool calls in Agent Builder, giving you complete control over how tool invocations are handled during agent runs.
-
**View Code for Workspace Scaffolding:** Added View Code support to scaffold a workspace for Foundry agents, letting you quickly generate the project structure needed to get started.
-
As part of the sidebar convergence between AI Toolkit and the Foundry extension, the Foundry sidebar will retire on June 1, 2026. All of its functionality has been moved into the AI Toolkit sidebar.
Set a calendar reminder for May 31. You have been warned.

---

## Practical Takeaways for .NET / Azure Teams

| Area | Action |
|---|---|
| **SDK** | Add `Azure.AI.Projects --prerelease`; start targeting the GA REST surface now |
| **Agents** | Prompt agents are GA; build on them first, then migrate to hosted/workflow agents as they graduate |
| **Networking** | Enable BYO VNet in Standard Setup before you go live — retrofitting private networking hurts |
| **Evaluations** | Wire up at least the built-in groundedness and safety evaluators before your first production deploy |
| **Models** | Nemotron Nano 9B is a viable low-cost option for tool-calling and coding-focused agents |
| **VS Code** | Update to AI Toolkit v0.32.0 and note the June 1 Foundry sidebar retirement |
| **AzureML SDK v1** | Migrate to SDK v2 before June 30, 2026 — that EOL is not a rumor |

---

## Further Reading

- https://blogs.microsoft.com/blog/2026/03/16/microsoft-at-nvidia-gtc-new-solutions-for-microsoft-foundry-azure-ai-infrastructure-and-physical-ai/
- https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/building-production-ready-secure-observable-ai-agents-with-real-time-voice-with-/4501074
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/nvidia%E2%80%99s-open-models-on-microsoft-foundry/4501643
- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/%F0%9F%9A%80-ai-toolkit-for-vs-code-%E2%80%94-march-2026-update/4502517
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-dec-2025-jan-2026/
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-feb-2026/
- https://learn.microsoft.com/en-us/windows/ai/toolkit/