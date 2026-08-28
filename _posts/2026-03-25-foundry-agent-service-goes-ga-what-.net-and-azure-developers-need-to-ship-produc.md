---
layout: post
title: "Foundry Agent Service Goes GA: What .NET and Azure Developers Need to Ship Production Agents Today"
date: 2026-03-25 07:57:43 -0400
tags: [agent, .net, actually, agents, api, claude-sonnet-4-6]
author: the.serf
---

## TL;DR

Microsoft's next-generation Foundry Agent Service is now **generally available**, bringing production-grade SDKs for .NET (and Python, JavaScript, Java), a Responses API-compatible runtime, end-to-end private networking, built-in evaluations, and an optional Voice Live layer—all under one roof. If you've been prototyping agents and wondering when the "production" switch flips, this is it.

---

## The Big News
Foundry Agent Service is now generally available, with production-ready SDKs across Python, JavaScript, Java, and .NET and a modern API for building durable, tool-using agents designed to streamline development while scaling reliably for enterprise workloads.
That last adjective—*enterprise*—is doing a lot of heavy lifting.
AI decision-makers cite security, privacy, and governance as the primary drivers for consolidating onto a unified platform, and report difficulty scaling AI across disconnected tools. Microsoft Foundry makes it easier to build enterprise agents by bringing enterprise-grade security, private networking, and observability to every agent you build.
In plain English: your team can stop duct-taping together an agent runner, a logging sink, a VNet config, and three separate NuGet packages—the platform now owns that plumbing.

---

## What Actually Shipped (Engineer Edition)

Here's the full GA bundle, distilled to what matters for your next sprint:

### 1. Responses API Runtime — OpenAI Wire-Compatible
Built on the Responses API — OpenAI's agentic API — agents are wire-compatible, so teams already using the Responses API can run on Foundry with minimal code changes, immediately gaining enterprise security, private networking, and observability.
This is significant. If you've been iterating against the OpenAI Responses API directly, your `AgentClient` calls won't need a rewrite — just point at your Foundry project endpoint and swap credentials.

### 2. Multi-Framework, Multi-Model Openness
The architecture is intentionally open. You're not locked to a single model provider or orchestration framework. Use a DeepSeek model for planning, an OpenAI model for generation, LangGraph for orchestration — the runtime handles the consistency layer. Agents, tools, and the surrounding infrastructure all speak the same protocol.
Microsoft Agent Framework is an open-source SDK for building multi-agent systems in code (for example, .NET and Python) with a cloud-provider-agnostic interface. Use Agent Framework when you want to define and orchestrate agents locally, and pair it with the Foundry SDK when you want those agents to run against Foundry models or when you want Agent Framework to orchestrate agents hosted in Foundry.
### 3. Three Agent Types — Pick Your Control Level
Foundry Agent Service is a fully managed platform for building, deploying, and scaling AI agents. Use any framework and many models from the Foundry model catalog. Create no-code prompt agents in the Foundry portal, or use the available SDKs and REST API to deploy them and code-based hosted agents built with Agent Framework, LangGraph, or your own code. Agent Service handles hosting, scaling, identity, observability, and enterprise security so you can focus on your agent logic.
| Agent Type | Status | Best For |
|---|---|---|
| Prompt (declarative) | ✅ GA | Rapid prototyping, internal tools |
| Hosted (code-based, containerized) | 🔬 Preview | Custom orchestration, multi-agent systems |
| Workflow agents | 🔬 Preview | Visual multi-step business logic |

### 4. End-to-End Private Networking + MCP Auth Expansion
Foundry Agent Service now ships with BYO VNet with no public egress, extended to cover tool connectivity — MCP servers, Azure AI Search, and Fabric data agents — plus MCP authentication expansion covering key-based, Entra Agent Identity, Managed Identity, and OAuth Identity Passthrough in a single service.
This directly addresses one of the biggest blockers for regulated industries: your agents can now call tools *and* retrieve data without a single packet leaving your private network.

### 5. Evaluations — Continuous, Not Just Pre-Ship
Foundry Evaluations are now generally available with three layers: out-of-the-box evaluators covering standard RAG and generation scenarios — coherence, relevance, groundedness, retrieval quality, and safety — with no custom configuration required. Custom evaluators let you encode your own criteria: business logic, internal tone standards, domain-specific compliance rules, or any quality signal that doesn't map cleanly to a general evaluator.
Running a test suite before shipping is not a production quality strategy — it's a snapshot. Quality degrades in production as traffic patterns shift, retrieved documents go stale, and new edge cases emerge that never appeared in your eval dataset.
The GA release wires evaluations into Azure Monitor so degradation shows up in your dashboards, not in your support queue.

### 6. Voice Live (Preview) — Real-Time Speech Agents
Voice Live integration with Agent Service is in public preview, enabling seamless real-time speech-to-speech interactions for agents in production.
Think IVR deflection, voice-first internal tools, or accessibility surfaces — now natively wired to your agent's prompt, tools, and tracing.

---

## .NET Quickstart: Zero to Running Agent

Install the GA SDK (pin to the `2.x` line):

```bash
dotnet add package Azure.AI.Projects --prerelease
```

Then bootstrap a prompt agent in C#:

```csharp
using Azure.AI.Projects;
using Azure.Identity;

var client = new AIProjectClient(
    new Uri(Environment.GetEnvironmentVariable("PROJECT_ENDPOINT")!),
    new DefaultAzureCredential());

var agentsClient = client.GetAgentsClient();

// Create a durable prompt agent
var agent = await agentsClient.CreateAgentAsync(
    model: "gpt-5.2",
    name: "MyProductionAgent",
    instructions: "You are a helpful assistant that answers questions about our product catalog.",
    tools: new List<ToolDefinition> { new FileSearchToolDefinition() }
);

Console.WriteLine($"Agent created: {agent.Value.Id}");
```

> ⚠️ **Note:**
You can find your endpoint in the overview for your project in the Microsoft Foundry portal, under **Libraries → Foundry**. Set this endpoint as an environment variable named `PROJECT_ENDPOINT`.
For LangGraph users on .NET,
teams building multi-agent workflows in LangGraph can now connect directly to the new Foundry Agent Service — composing complex graphs while keeping individual agents running in Foundry. Developers stay in their existing LangGraph environment and get access to tools like Foundry IQ, code interpreter, MCP, and file search, with all agents visible and governed in Foundry Control Plane and end-to-end observability across the full graph.
---

## Tooling Update: AI Toolkit for VS Code v0.32.0

The GA release pairs neatly with a fresh VS Code tooling update.
Version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents, bringing a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
The headline UX change:
the Foundry sidebar has been merged directly into AI Toolkit, allowing you to access the power of both extensions. The AI Toolkit and Foundry extension sidebar panels have been unified into a single **My Resources** view — local resources (models, agents, tools) are grouped under a Local Resources node, with Foundry remote resources appearing right alongside them.
Note that the Foundry sidebar will retire on June 1st, 2026, and all of its functionalities have been moved into the AI Toolkit sidebar
— so now is a good time to consolidate your VS Code extension list.

For agent development control freaks (the best kind),
MCP Tool Approval lets you configure auto or manual approval for MCP tool calls in Agent Builder, giving you complete control over how tool invocations are handled during agent runs.
---

## One Migration Warning You Shouldn't Miss
The Azure Machine Learning SDK v1 reaches end of support on **June 30, 2026**. After this date, existing workflows may face security risks and breaking changes without active Microsoft support.
If you have training pipelines still running on AzureML SDK v1, the clock is ticking louder than ever. Migrate to SDK v2 now —
the SDK v2 migration guide is the place to start; v2 brings a significantly improved authoring experience, YAML-first job definitions, and continued investment from the Azure ML team.
---

## Practical Takeaways

- **Start on the `azure-ai-projects` 2.x SDK today** — it's the single package for agents, inference, evaluations, and memory going forward. The old `azure-ai-agents` dependency is gone.
- **Enable private networking before you go to production** — BYO VNet now covers tool calls, not just model inference. Your security team will thank you.
- **Wire evaluations into your CI/CD pipeline** — out-of-the-box evaluators require zero custom configuration; there's no excuse not to gate deployments on groundedness scores.
- **Merge your VS Code extensions** — the Foundry sidebar retires June 1, 2026. Get comfortable in the unified AI Toolkit sidebar now.
- **Migrate AzureML SDK v1 before June 30, 2026** — this is a hard deadline, not a suggestion.

---

## Further Reading

- https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/building-production-ready-secure-observable-ai-agents-with-real-time-voice-with-/4501074
- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/🚀-ai-toolkit-for-vs-code-—-march-2026-update/4502517
- https://blogs.microsoft.com/blog/2026/03/16/microsoft-at-nvidia-gtc-new-solutions-for-microsoft-foundry-azure-ai-infrastructure-and-physical-ai/
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-feb-2026/
- https://learn.microsoft.com/en-us/azure/foundry/agents/overview
- https://learn.microsoft.com/en-us/azure/foundry/how-to/develop/sdk-overview
- https://www.infoq.com/news/2026/01/azure-functions-mcp-support/