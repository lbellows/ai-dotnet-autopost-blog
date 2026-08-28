---
layout: post
title: "AI Toolkit for VS Code v0.32.0: Your New Production Agent Command Center"
date: 2026-03-17 07:56:32 -0400
tags: [foundry, cli, agent, code, extension, claude-sonnet-4-6]
author: the.serf
---

## TL;DR

Microsoft shipped **AI Toolkit for VS Code v0.32.0** on March 17, 2026 — the freshest ink on the page. It unifies agent creation, MCP tool-call approval, Foundry workspace scaffolding, and GitHub Copilot-powered evaluations into a single sidebar. Pair that with **GPT-5.3-Codex** now rolling out in Microsoft Foundry, and .NET teams have a genuinely end-to-end path from local prototype to production agent — without ever leaving VS Code. Here's what changed, why it matters, and how to wire it up today.

---

## What Just Dropped
Version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents, bringing a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
The most important headline buried in the release notes:
the standalone Foundry sidebar extension will retire on June 1st, 2026, and all of its functionality has been moved into the AI Toolkit sidebar.
If you have both extensions installed, you now have one fewer thing cluttering your Activity Bar. Celebrate the small victories.

---

## The Three Changes That Actually Matter

### 1. A Unified "Create Agent" Entry Point
The new Create Agent View serves as a unified entry point for creating AI agents. It offers two distinct paths side by side: "Create in Code with Full Control" — scaffold a project from a template, or generate a single agent or multi-agent workflow using GitHub Copilot — and "Design an Agent Without Code" — launch Agent Builder directly to configure a prompt agent through the UI.
For .NET teams, this is meaningful:
you can generate agent code in Python **or .NET**, giving you flexibility to target your preferred runtime.
No more being a second-class citizen behind the Python crowd.

### 2. MCP Tool-Call Approval
You can now configure auto or manual approval for MCP tool calls in Agent Builder, giving you complete control over how tool invocations are handled during agent runs.
This is the guardrail .NET developers deploying into regulated environments have been asking for. Set approval to `manual` during development (so you can eyeball each tool invocation), then flip to `auto` once you trust the flow. Think of it as a feature-flag for your agent's autonomy dial.

### 3. Foundry Workspace Scaffolding via "View Code"
View Code support was added to scaffold a workspace for Foundry agents, letting you quickly generate the project structure needed to get started.
Behind the scenes,
agent code generation, evaluation, and deployment now uses the open-source Microsoft Foundry skill from the same source used by GitHub Copilot for Azure, and AI Toolkit automatically installs and keeps this skill up to date, requiring no manual setup.
---

## The Model That Pairs With It: GPT-5.3-Codex

The tooling update lands alongside a model update worth knowing about.
Starting this week, GPT-Realtime-1.5, GPT-Audio-1.5, and GPT-5.3-Codex are rolling out into Microsoft Foundry — models that reflect the growing needs of the modern developer and push from short, stateless interactions toward AI systems that can reason, act, and collaborate over time.
GPT-5.3-Codex brings together advanced coding capability with broader reasoning and professional problem solving in a single model built for real engineering work. It unifies the frontier coding performance of GPT-5.2-Codex with the reasoning and professional knowledge capabilities of GPT-5.2 in one system — shifting the experience from optimizing isolated outputs to supporting longer-running development efforts where repositories are large, changes span multiple steps, and requirements aren't always fully specified at the start.
> **A word of caution:**
Registration is required for access to `gpt-5.3-codex`, and access will be granted based on Microsoft's eligibility criteria.
Don't build your sprint plan around it until you've confirmed your subscription is approved.

---

## Getting Started: CLI & SDK Snippets

### Install / Update the Extension

```bash
# From the VS Code command palette:
# Extensions: Install Extension → "AI Toolkit"
# Or via CLI:
code --install-extension ms-windows-ai-studio.windows-ai-studio
```

### Wire Up Codex CLI Against Your Foundry Endpoint
To use Codex CLI with Azure, you need to create and set up a `config.toml` file stored in the `~/.codex` directory. Create the file inside that directory or edit the existing file.
With the v1 Responses API, you no longer need to pass `api-version`, but you must include `/v1` in the `base_url` path.
```toml
# ~/.codex/config.toml
model            = "gpt-5.3-codex"   # your Foundry deployment name
model_provider   = "azure"
wire_api         = "responses"

[model_providers.azure]
name     = "Azure OpenAI"
base_url = "https://YOUR_RESOURCE.openai.azure.com/openai/v1"
env_key  = "AZURE_OPENAI_API_KEY"
```

```bash
export AZURE_OPENAI_API_KEY="<your-key>"
codex "Refactor this .NET 9 minimal API to use keyed DI services"
```

### Scaffold a Foundry Agent Project in .NET

Inside AI Toolkit v0.32.0, hit **Create Agent → Create in Code → Generate with Copilot**, choose **.NET**, and the Foundry skill generates your starter project — `Program.cs`, tool definitions, and an eval harness — in seconds.

---

## The Foundry SDK Is Almost GA — Don't Sleep On the Migration

The tooling upgrade is happening in concert with a broader SDK consolidation.
SDK releases across all languages — Python (2.0.0b4), .NET (2.0.0-beta.1), JS/TS (2.0.0-beta.4), and Java (2.0.0-beta.1) — all shipped new betas targeting the GA REST surface with significant breaking changes including tool class renames, credential updates, and preview feature opt-in flags.
March is shaping up to be a big one — SDK GA announcements are on the horizon, and the Foundry SDK will be the single package you need across agents, inference, evaluations, and memory. Get ahead of it now by upgrading to the latest pre-release and targeting the GA REST surface.
For .NET specifically, bump your package reference now and iron out any breaking changes before GA day drops:

```xml
<!-- .csproj -->
<PackageReference Include="Azure.AI.Projects" Version="2.0.0-beta.1" />
```

---

## Practical Takeaways

| What | Why it matters to you |
|---|---|
| AI Toolkit v0.32.0 | Unified sidebar, MCP approval controls, .NET scaffolding — install today |
| Foundry sidebar retiring June 1 | Consolidate to AI Toolkit now; don't wait |
| GPT-5.3-Codex rolling out | Request access early; gated by subscription approval |
| Foundry .NET SDK 2.0.0-beta.1 | Breaking changes now, GA SLAs soon — migrate in dev before production is forced |
| Evaluation as Tests | CI-friendly, versioned agent evals via `pytest-agent-evals` SDK |

---

## Further Reading

- AI Toolkit for VS Code — March 2026 Update (v0.32.0): https://techcommunity.microsoft.com/blog/azuredevcommunityblog/%F0%9F%9A%80-ai-toolkit-for-vs-code-%E2%80%94-march-2026-update/4502517
- New Azure OpenAI Models in Microsoft Foundry (GPT-5.3-Codex, GPT-Realtime-1.5): https://techcommunity.microsoft.com/blog/azure-ai-foundry-blog/new-azure-open-ai-models-bring-fast-expressive-and-real%E2%80%91time-ai-experiences-in-m/4496184
- What's New in Microsoft Foundry — February 2026 (SDK betas, Foundry REST GA): https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-feb-2026/
- Codex with Azure OpenAI in Microsoft Foundry Models (how-to, config.toml): https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/codex
- Foundry Models Sold Directly by Azure (gating & registration info): https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure
- Generative AI with LLMs in C# in 2026 (.NET blog foundations piece): https://devblogs.microsoft.com/dotnet/generative-ai-with-large-language-models-in-dotnet-and-csharp/