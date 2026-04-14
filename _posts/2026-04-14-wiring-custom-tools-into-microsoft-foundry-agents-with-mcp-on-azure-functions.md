---
layout: post
title: "Wiring Custom Tools into Microsoft Foundry Agents with MCP on Azure Functions"
date: 2026-04-14 08:07:57 -0400
tags: [agent, azure, .net, architecture, attach, gpt-5.2-chat]
author: the.serf
---

**TL;DR**  
Microsoft just showed a clean, production-ready path for giving Foundry agents real superpowers: host your custom tools as **Model Context Protocol (MCP)** servers on **Azure Functions**, then plug them directly into **Microsoft Foundry Agent Service**. The result is reusable tools, enterprise auth, and serverless pricing—without rewriting SDK glue code. If you build agents on .NET or Azure, this is the most practical update you should care about this week. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

---

## The laser-focused update

On **April 8, 2026**, Microsoft published a step‑by‑step guide showing how to connect an MCP server hosted on **Azure Functions** to **Microsoft Foundry agents**. The post isn’t marketing fluff—it’s an architectural unlock. You can now expose your internal APIs, databases, or business logic as MCP tools once and reuse them across:

- Foundry-hosted agents
- IDE copilots (VS Code, Visual Studio, Cursor)
- Any MCP‑compatible client

No re‑authoring tools per surface, no bespoke function-calling adapters. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

---

## Why this matters to .NET & Azure engineers

### 1) Tool reuse without SDK sprawl
If you already run an MCP server locally for Copilot or Claude, hosting the same server on Azure Functions means **the agent becomes just another client**. The MCP contract stays stable while your consumers multiply. That’s a rare win for maintainability. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

### 2) Enterprise auth that doesn’t hurt
The guide walks through **OAuth Identity Passthrough** and managed identity options, so agents can call tools **on behalf of the user** with Entra ID enforcing access. This keeps security teams calm and auditors asleep. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

### 3) Serverless cost model for agent tools
Azure Functions gives you:
- Scale-to-zero when agents are idle
- Pay-per-execution pricing
- No container babysitting

For agent tools that are bursty (most are), this is materially cheaper than always-on services. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

---

## Architecture at a glance

```
Foundry Agent
   └── MCP Tool Call
        └── Azure Functions (MCP Server)
              ├── Entra ID / OAuth
              ├── Internal APIs
              └── Databases / Services
```

The agent discovers tools dynamically, decides when to call them, and folds the results back into its reasoning loop. You write the tool once; the agent does the orchestration. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

![Wiring Custom Tools into Microsoft Foundry Agents with MCP on Azure Functions...](https://i.imgflip.com/ap5agf.jpg)

---

## Practical setup (condensed)

### 1) Host an MCP server on Azure Functions
Azure Functions is a natural fit because MCP relies on HTTP + server‑sent events (SSE), both supported natively. You deploy your MCP endpoints just like any other function app. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

### 2) Secure it
Choose one:
- **Managed Identity** (best for service‑to‑service)
- **OAuth Identity Passthrough** (best for user‑aware agents)

The blog includes concrete configuration steps for Entra ID. ([devblogs.microsoft.com](https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/))

### 3) Attach it to a Foundry agent
In Foundry Agent Service, you register the MCP server endpoint. The agent automatically imports the tools and keeps them updated as your server evolves. No redeploying the agent for every new function. ([devblogs.microsoft.com](https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/))

---

## Latency, reliability, and limits

- **Latency:** Azure Functions adds a small cold-start penalty, but for most tool calls (DB queries, workflow triggers) this is negligible compared to LLM inference time.
- **Reliability:** Foundry Agent Service GA now supports private networking and multiple MCP auth modes, reducing failure modes in locked-down enterprises. ([devblogs.microsoft.com](https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/))
- **Limits:** MCP tools are best for *actions and retrieval*, not long-running batch jobs. Keep tools tight and deterministic.

---

## What this changes in practice

Before:  
> “We need custom function calling for each agent and each client.”

Now:  
> “We expose capabilities once via MCP and let agents discover them.”

For teams shipping on **.NET + Azure**, this is a strong signal: **agent tooling is becoming infrastructure**, not application glue. If you invest here, you’re future‑proofing across IDEs, agents, and platforms.

---

## Further reading

- https://devblogs.microsoft.com/azure-sdk/give-your-foundry-agent-custom-tools-with-mcp-servers-on-azure-functions/  
- https://devblogs.microsoft.com/foundry/foundry-agent-service-ga/  
- https://devblogs.microsoft.com/foundry/announcing-model-context-protocol-support-preview-in-azure-ai-foundry-agent-service/