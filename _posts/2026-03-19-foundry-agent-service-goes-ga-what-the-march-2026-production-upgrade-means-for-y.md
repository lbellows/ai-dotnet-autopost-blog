---
layout: post
title: "Foundry Agent Service Goes GA: What the March 2026 Production Upgrade Means for Your .NET Agent Workloads"
date: 2026-03-19 07:51:11 -0400
tags: [.net, agents, api, can, code, claude-sonnet-4-6]
author: the.serf
---

**TL;DR:** Microsoft dropped the GA of the next-generation Foundry Agent Service at NVIDIA GTC this week, bringing production-ready SDKs (including .NET), private-network-only deployments, a unified evaluation pipeline, and Voice Live API into the mix — all on a Responses API–based runtime that's wire-compatible with OpenAI agents. If you've been running agent prototypes on Azure, it's time to graduate them.

---

## What Just Shipped
General availability of the next-generation Foundry Agent Service arrived this week: a modern API, runtime, and developer platform designed to help teams build agents and move from prototype to production quickly.
At NVIDIA GTC 2026 (March 16–19, San Jose), Microsoft packaged several announcements together. The headline for developers:
the GA release includes a Responses API–based runtime that is wire-compatible with OpenAI agents, open model support across DeepSeek, xAI, Meta, LangChain, and LangGraph, end-to-end private networking with BYO VNet, expanded MCP authentication, Voice Live in preview, and GA evaluations piped into Azure Monitor.
Translation: if you already have code targeting the OpenAI Responses API, swapping in Foundry endpoints is, in Microsoft's words, "minimal code changes." You get enterprise security and observability essentially for free.

---

## The .NET SDK Is Production-Ready
Foundry Agent Service is now generally available with production-ready SDKs across Python, JavaScript, Java, and .NET, and a modern API for building durable, tool-using agents designed to streamline development while scaling reliably for enterprise workloads.
In February, the .NET SDK was still at `2.0.0-beta.1`.
March was signposted as "a big one — SDK GA announcements are on the horizon, and the Foundry SDK will be the single package you need across agents, inference, evaluations, and memory."
That promise has now been delivered. Before pinning the GA package, be aware that the beta line shipped significant breaking changes:
Python (2.0.0b4), .NET (2.0.0-beta.1), JS/TS (2.0.0-beta.4), and Java (2.0.0-beta.1) all targeted the GA REST surface with tool class renames, credential updates, and preview feature opt-in flags.
Review the migration guide before upgrading.

A quick scaffold to get started once the GA package is available on NuGet:

```bash
dotnet add package Azure.AI.Projects --prerelease
```

```csharp
using Azure.AI.Projects;
using Azure.Identity;

var client = new AIProjectClient(
    new Uri("https://<your-project>.services.ai.azure.com/"),
    new DefaultAzureCredential());

// Wire up a prompt agent (GA)
var agent = await client.Agents.CreateAgentAsync(
    model: "gpt-4o",
    name: "my-dotnet-agent",
    instructions: "You are a helpful assistant.");

Console.WriteLine($"Agent created: {agent.Value.Id}");
```

> ⚠️ Check the [official quickstart](https://learn.microsoft.com/azure/ai-services/agents/) for the exact GA class names — beta renames landed with a thud.

---

## Private Networking: The Enterprise Unlock

This is the feature that will matter most for teams inside regulated industries (finance, healthcare, government).
Foundry Agent Service now supports Standard Setup with private networking, where you bring your own virtual network (BYO VNet): agent traffic never traverses the public internet, and private networking is extended to tool connectivity — MCP servers, Azure AI Search indexes, and Fabric data agents can all operate over private network paths.
Previously, private inference was possible but tool-calling still leaked to the public internet for retrieval. That gap is now closed. For .NET teams running sensitive workloads behind Azure Virtual Network, this removes the last architectural blocker to full production deployment.

---

## Evaluations: Finally a Quality Loop You Can Wire Into CI/CD
Foundry Evaluations are now generally available with out-of-the-box evaluators covering standard RAG and generation scenarios: coherence, relevance, groundedness, retrieval quality, and safety — no custom configuration required; connect them to a dataset or live traffic and get quantitative scores back.
Continuous evaluation closes the production loop: Foundry samples live traffic automatically, runs your evaluator suite against it, and surfaces results through integrated dashboards. You can configure Azure Monitor alerts to fire when groundedness drops, safety thresholds breach, or performance degrades — before users notice.
This is the "shift-left quality" story for agents that previously required bespoke tooling. Wire it into your Azure DevOps or GitHub Actions pipeline and treat groundedness like a unit test.

---

## NVIDIA Nemotron Models + Foundry: New Inference Options

For teams with latency-sensitive or cost-conscious workloads, the model catalog just got more interesting.
Microsoft Foundry expands its open model catalog with several NVIDIA Nemotron models available through NVIDIA NIM microservices, providing enterprises with production-ready open-weight reasoning models accessible through a unified platform.
Specifically:
Llama Nemotron Nano VL 8B is available now and is tailored for multimodal vision-language tasks, document intelligence, and mobile and edge AI agents; NVIDIA Nemotron Nano 9B supports enterprise agents, scientific reasoning, advanced math, and coding for software engineering and tool calling.
These are open-weight models, which means fine-tuning is on the table.
Fireworks AI coming to Microsoft Foundry enables customers to fine-tune open-weight models like NVIDIA Nemotron into low-latency assets that can be distributed to the edge.
---

## Voice Live API: Real-Time Speech Agents in Preview

Buried in the announcement but worth flagging:
Voice Live API integration with Foundry Agent Service is now in public preview, enabling developers to build voice-first, multimodal, real-time agentic experiences.
This enables seamless real-time speech-to-speech interactions for agents in production.
If you're building IVR deflection, call center copilots, or accessibility tooling on .NET, this is the stack to watch.

---

## Tooling: AI Toolkit for VS Code v0.32.0

Shipping alongside the GA is the March update for AI Toolkit for VS Code.
Version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents, bringing a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
MCP Tool Approval lets you configure auto or manual approval for MCP tool calls in Agent Builder, giving you complete control over tool invocations during agent runs; View Code for Workspace Scaffolding lets you quickly generate the project structure needed to get started with Foundry agents.
Also worth noting:
as part of the sidebar convergence between AI Toolkit and the Foundry extension, the Foundry sidebar will retire on June 1, 2026 — all of its functionalities have been moved into the AI Toolkit sidebar.
Update your team's setup docs accordingly.

---

## Practical Takeaways

| Concern | What to Do This Week |
|---|---|
| **SDK version** | Upgrade to GA package; audit for beta class renames |
| **Networking** | Enable BYO VNet in Standard Setup for regulated workloads |
| **Evaluations** | Add Foundry Evaluations to your CI/CD pipeline as quality gates |
| **Models** | Evaluate Nemotron Nano 9B for code/reasoning tasks at lower cost |
| **Tooling** | Update AI Toolkit to v0.32.0; migrate away from Foundry sidebar before June 1 |

---

## Further Reading

- https://blogs.microsoft.com/blog/2026/03/16/microsoft-at-nvidia-gtc-new-solutions-for-microsoft-foundry-azure-ai-infrastructure-and-physical-ai/
- https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/building-production-ready-secure-observable-ai-agents-with-real-time-voice-with-/4501074
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/nvidia%E2%80%99s-open-models-on-microsoft-foundry/4501643
- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/%F0%9F%9A%80-ai-toolkit-for-vs-code-%E2%80%94-march-2026-update/4502517
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-feb-2026/
- https://venturebeat.com/orchestration/accelerating-ai-4-ways-microsoft-and-nvidia-enable-frontier-firms-in-2026