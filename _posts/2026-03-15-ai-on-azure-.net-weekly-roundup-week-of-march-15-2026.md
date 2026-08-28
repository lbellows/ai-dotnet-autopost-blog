---
layout: post
title: "AI on Azure & .NET: Weekly Roundup — Week of March 15, 2026"
date: 2026-03-15 07:40:46 -0400
tags: [agent, microsoft, azd, foundry, new, claude-sonnet-4-6]
author: the.serf
---

**TL;DR:** GPT-5.4 lands in Microsoft Foundry with built-in computer-use, Agent 365 gets a GA date and a price tag, the Microsoft Agent Framework hits Release Candidate for .NET and Python, old GPT-4o model versions sunset on March 31, and the `azd` CLI now lets you diagnose hosted agents without leaving your terminal. Buckle up — this was a dense week.

---

## 1. GPT-5.4 is Generally Available in Microsoft Foundry

The headline model drop this week:
GPT-5.4 is now generally available in Microsoft Foundry, designed to help organizations move from planning work to reliably completing it in production environments. As AI agents are applied to longer, more complex workflows, consistency and follow-through become as important as raw intelligence — and GPT-5.4 combines stronger reasoning with built-in computer-use capabilities to support automation scenarios and dependable execution across tools, files, and multi-step workflows at scale.
For engineers who care about the fine print,
key improvements include more consistent reasoning over time, enhanced instruction alignment to reduce prompt tuning and oversight, latency-improved performance for responsive real-time workflows, and integrated computer-use capabilities for structured orchestration of tools, file access, data extraction, guarded code execution, and agent handoffs.
There is also a premium variant:
GPT-5.4 Pro is designed for scenarios where analytical depth and completeness are prioritized over latency — organizations typically select GPT-5.4 Pro when deeper analysis is required (such as scientific research and complex problems), while GPT-5.4 remains the right choice for workloads that prioritize reliable execution and agentic follow-through.
> **Access note:**
Registration is required for access to GPT-5.4 and GPT-5.4-Pro.
Plan ahead if you're evaluating it for a production rollout.

**Practical takeaway for .NET devs:**
When evaluating or switching to GPT-5 family models in .NET apps, keep quality checks consistent across unit tests and CI by using `Microsoft.Extensions.AI.Evaluation` — it provides quality, safety, and NLP evaluators with caching and reporting to help you spot regressions before rollout.
---

## 2. ⚠️ Model Retirement Deadline: March 31 is NOT a Drill

If you're running GPT-4o on Azure, this section is for you (no skimming allowed).
Azure OpenAI has officially announced the retirement of GPT-4o base model versions 2024-05-13 and 2024-08-06. These versions apply to base (non-fine-tuned) deployments, including those consumed through the Assistants API (Preview). After the retirement date, these GPT-4o versions will no longer be deployable, callable, or operable in Azure OpenAI.
The official replacement model for GPT-4o in the Assistants API (Preview) is `gpt-5.1` version 2025-11-13. This model is the supported successor for agent-style, tool-using, threaded Assistant workloads, and it fully replaces GPT-4o for this use case.
**Migration quick-start (no code changes required, just a new deployment):**

```bash
# Create the new gpt-5.1 deployment
az cognitiveservices account deployment create \
  --name "<your-aoai-resource>" \
  --resource-group "<your-rg>" \
  --deployment-name "gpt-51-assistant" \
  --model-name "gpt-5.1" \
  --model-version "2025-11-13" \
  --model-format "OpenAI" \
  --sku-name "GlobalStandard" \
  --sku-capacity 10
```

One important nuance:
when OpenAI announces that a model is retired from ChatGPT or OpenAI-hosted APIs, that retirement does not automatically apply to Azure AI Foundry. For any workload running on Azure AI Foundry, the Foundry portal (Model Catalog → Retirement date column) and the Microsoft Learn "Azure OpenAI in Foundry – Model Retirements" documentation are the only authoritative sources — OpenAI blog posts or ChatGPT retirement notices should not be used to determine Azure retirement timelines.
Also watch out when upgrading:
when upgrading from previous reasoning models to `gpt-5.1`, you may need to update your code to explicitly pass a `reasoning_effort` level if you want reasoning to occur.
---

## 3. Microsoft Agent Framework Hits Release Candidate
Microsoft Agent Framework is now at Release Candidate — a stable, open-source foundation for building single and multi-agent systems across .NET and Python, on the path to GA.
For .NET engineers, the framework leans into patterns you already know:
Microsoft Agent Framework doesn't force you to learn a completely new way of doing things — it builds on familiar concepts like dependency injection, middleware, and telemetry, making it feel natural for C# developers.
One of the more compelling patterns is the handoff model:
instead of one agent doing everything, you can split work across multiple specialized agents. In the handoff pattern, one agent transfers full control of the conversation to the next — the receiving agent takes over entirely. This is different from "agent-as-tools," where a primary agent calls others as helpers but retains control.
Want stateful agents that survive restarts?
The durable task extension for Microsoft Agent Framework enables you to build stateful AI agents and multi-agent deterministic orchestrations in a serverless environment on Azure. The durable task extension provides durable state management, meaning your agent's conversation history and execution state are reliably persisted and survive failures, restarts, and long-running operations. Durable agents combine the power of Agent Framework with Azure Durable Functions to persist state automatically across function invocations and resume after failures without losing conversation context.
**Kick the tires in under 5 minutes:**

```bash
# Scaffold a .NET agent project with azd
azd init -t Azure-Samples/azd-ai-starter-basic --location northcentralus

# Wire in your Foundry credentials
dotnet user-secrets --file ./apphost.cs set MicrosoftFoundry:Project:Endpoint "<your-endpoint>"
dotnet user-secrets --file ./apphost.cs set MicrosoftFoundry:Project:ApiKey "<your-key>"

# Run locally with Aspire
aspire run --file ./apphost.cs

# Ship to Azure Container Apps
azd up
```

> **Note:**
Hosted agents are currently limited to the North Central US Azure region.
Keep that in mind when planning latency-sensitive deployments.

---

## 4. Agent 365 GA + Microsoft 365 E7 — What It Means for Your Org

This one is more platform strategy than pure dev news, but the implications for teams building agentic apps are real.
Microsoft has announced the May 1 general availability of Microsoft Agent 365, the control-plane for AI agents. Priced at $15 per user, Agent 365 gives IT and security leaders a single place to observe, govern, manage, and secure agents across the organization — using the same infrastructure, applications, and protections they rely on to manage people today.
The scale numbers are eye-catching:
tens of millions of agents appeared in the Agent 365 Registry within just two months of preview availability, and tens of thousands of customers have already begun adopting the platform.
From the developer side, the SDK is explicitly framework-agnostic:
Agent 365 works with agents built on any agent SDK or platform — including low-code platforms like Copilot Studio and Azure AI Foundry, as well as pro-code options such as Microsoft Agent Framework, Microsoft Agents SDK, OpenAI Agents SDK, Claude Code SDK, and LangChain SDK.
The Agent 365 SDK enhances agents you've already built — regardless of the underlying stack — by adding enterprise capabilities such as Entra-based agent identity, governed MCP tool access, OpenTelemetry-based observability, notifications through the Activity protocol, and agent ID-driven governance.
---

## 5. New `azd` CLI Commands for AI Agent Observability

A small but genuinely useful quality-of-life drop from the Azure Developer CLI team:
new `azd ai agent show` and `azd ai agent monitor` commands help you diagnose hosted AI agent failures directly from the CLI.
And when you deploy via `azd`, observability is wired up automatically:
hosted agents support exposing OpenTelemetry traces, metrics, and logs from underlying frameworks to Microsoft Foundry with Application Insights or any user-specified OpenTelemetry Collector endpoint. If you use the `azd ai agent` CLI extension, Application Insights is automatically provisioned and connected to your Foundry project. Your project's managed identity is granted the Azure AI User role on the Foundry resource so that traces are exported to Application Insights.
---

## 6. Real-Time Voice Models and the GPT-5.3-Codex Rollout
GPT-Realtime-1.5, GPT-Audio-1.5, and GPT-5.3-Codex are rolling out into Microsoft Foundry. Together, these models push the needle from short, stateless interactions toward AI systems that can reason, act, and collaborate over time.
For voice-first .NET apps, the improvement numbers are concrete:
in OpenAI's evaluations, GPT-Realtime-1.5 shows a +5% lift on Big Bench Audio (reasoning), a +10.23% improvement in alphanumeric transcription, and a +7% gain in instruction following, while maintaining low-latency performance.
GPT-5.3-Codex brings together advanced coding capability with broader reasoning and professional problem solving. It unifies the frontier coding performance of GPT-5.2-Codex with the reasoning and professional knowledge capabilities of GPT-5.2 in one system — shifting the experience from optimizing isolated outputs to supporting longer-running development efforts where repositories are large and changes span multiple steps.
---

## What's on the Horizon

- **AzureML SDK v1 End of Life is June 30, 2026.**
Migrate to SDK v2 now — CLI v1 already sunset in September 2025, and `azure-ai-projects` v2 beta has unified agents, inference, evaluations, and memory in a single package.
- **Agent 365 + M365 Copilot Wave 3** goes live May 1, including
expanded model diversity from both OpenAI and Anthropic.
- **MCP + Azure Functions GA** is worth re-evaluating if you shelved it during preview:
the Premium plan supports "always-ready" instances that remain pre-initialized, eliminating cold-start latency — critical for mission-critical tools where initialization delays can cause SSE connection timeouts and poor agent response times.
- **Fine-tuning on gpt-4o (2024-05-13 / 2024-08-06) is blocked as of March 31.**
Existing fine-tuned GPT-4o deployments are allowed to continue running for an additional one-year grace period, but Microsoft strongly recommends migrating as early as possible.
---

## Further Reading

- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/introducing-gpt-5-4-in-microsoft-foundry/4499785
- https://blogs.microsoft.com/blog/2026/03/09/introducing-the-first-frontier-suite-built-on-intelligence-trust/
- https://venturebeat.com/technology/microsoft-says-ungoverned-ai-agents-could-become-corporate-double-agents-its
- https://devblogs.microsoft.com/foundry/microsoft-agent-framework-reaches-release-candidate/
- https://developer.microsoft.com/blog/build-a-real-world-example-with-microsoft-agent-framework-microsoft-foundry-mcp-and-aspire
- https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/extensions/azure-ai-foundry-extension
- https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents
- https://learn.microsoft.com/en-us/answers/questions/5720653/replacement-models-for-retired-models-supporting-a
- https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/new-azure-open-ai-models-bring-fast-expressive-and-real%E2%80%91time-ai-experiences-in-m/4496184
- https://www.infoq.com/news/2026/01/azure-functions-mcp-support/
- https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-dec-2025-jan-2026/
- https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure
- https://learn.microsoft.com/en-us/microsoft-agent-365/developer/
- https://devblogs.microsoft.com/dotnet/upgrading-to-microsoft-agent-framework-in-your-dotnet-ai-chat-app/