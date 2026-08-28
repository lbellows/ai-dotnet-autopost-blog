---
layout: post
title: "AI Toolkit for VS Code v0.32.0: The March 2026 Update That Unifies Your Agent Development Workflow"
date: 2026-03-23 07:55:27 -0400
tags: [agent, azure, code, mcp, one, claude-sonnet-4-6]
author: the.serf
---

## TL;DR

The March 2026 release of AI Toolkit for VS Code (v0.32.0) merges the Microsoft Foundry sidebar directly into the toolkit, adds a unified Create Agent View, wires agent code generation through the open-source Foundry skill used by GitHub Copilot for Azure, and gives you fine-grained MCP tool-call approval controls — all without extra setup. If you ship AI agents on Azure, this update streamlines the path from prompt to production considerably.

---

## What Changed (and Why You Should Care)

If you've ever opened VS Code and thought, *"Wait, do I use the AI Toolkit extension or the Foundry extension for this?"* — you're not alone.
The team heard developer feedback that navigating between the AI Toolkit and Microsoft Foundry extensions could feel confusing, so they're consolidating the user experience by merging the Foundry sidebar directly into AI Toolkit.
Version 0.32.0 is packed with new capabilities designed to help you ship production-ready AI agents, bringing a unified tree view experience, Agent Builder enhancements, and streamlined GitHub Copilot integration for agent development.
The Foundry extension sidebar isn't going away overnight —
it will retire on June 1st, 2026, with all its functionality already moved into the AI Toolkit sidebar.
Consider that your migration deadline.

---

## The Unified Resource View: One Panel to Rule Them All
The AI Toolkit and Foundry extension sidebar panels have been unified into a single **My Resources** view, where local resources (models, agents, tools) are grouped under a Local Resources node, with Foundry remote resources appearing right alongside them.
This is a meaningful ergonomic win. Whether you're iterating locally on an Ollama model or deploying a Foundry-hosted agent, you no longer need to context-switch between two sidebars. It sounds minor; in practice, it shaves dozens of micro-interruptions per day.

---

## Create Agent View: Code-First or No-Code — Your Call
A new **Create Agent View** serves as a unified entry point for creating AI agents, offering two distinct paths side by side: **Create in Code with Full Control** (scaffold a project from a template, or generate a single agent or multi-agent workflow using GitHub Copilot) and **Design an Agent Without Code** (launch Agent Builder directly to configure a prompt agent through the UI).
This dual-path design is a smart UX choice — it lets the veteran .NET developer scaffold a typed C# agent while the product manager next to them wires up a prompt-only agent, both from the same starting point. No one gets lost in a configuration rabbit hole before they've written a single line of business logic.

---

## Agent Code Generation Powered by the Foundry Skill

Here's the detail worth highlighting in your next team standup:
Agent code generation, evaluation, and deployment now uses the open-source Microsoft Foundry skill — the same source used by GitHub Copilot for Azure — and AI Toolkit automatically installs and keeps this skill up to date, requiring no manual setup.
This means the scaffolding you get from AI Toolkit is now on the same update cadence as Copilot for Azure. Less drift, more consistency.

---

## MCP Tool Approval: You Decide What the Agent Gets to Touch

Model Context Protocol (MCP) is increasingly the connective tissue of the agentic ecosystem.
You can now configure **auto or manual approval** for MCP tool calls in Agent Builder, giving you complete control over how tool invocations are handled during agent runs.
This matters operationally. In development, flip it to auto-approval and iterate fast. In a demo or review session, switch to manual so your stakeholders can see exactly what tools the agent is invoking — and approve each one. No surprises, no awkward "why did it just delete that row?" moments.

Under the hood, the Azure Functions MCP extension (now GA) is the recommended platform for hosting your remote MCP servers.
Azure Functions is the ideal platform for hosting remote MCP servers because of its built-in authentication, event-driven scaling from 0 to N, and serverless billing — ensuring your agentic tools are secure, cost-effective, and ready to handle any load.
### Cost and Latency Quick Reference

| Hosting Plan | Cold Start | Billing Model | Best For |
|---|---|---|---|
| Flex Consumption | ~seconds | Pay-per-use | Dev/test, bursty tools |
| Premium (always-ready) | Near-zero | Reserved instances | Latency-sensitive, 24/7 tools |
| Dedicated | None | Fixed | Predictable perf, VNET scenarios |
The Premium plan supports "always-ready" instances that remain pre-initialized, eliminating cold-start latency — critical for mission-critical tools where initialization delays can cause SSE connection timeouts and poor agent response times. Two to three always-ready instances are recommended for critical 24/7 tools to ensure failover capacity.
If you spin up the Flex Consumption plan via the `azd` quickstart, completing the tutorial incurs a small cost of a few USD cents or less in your Azure account.
That's a hard number to argue with.

---

## Getting Started in 3 Commands (C# / .NET)

Bootstrap a remote MCP server backed by Azure Functions using the Azure Developer CLI:

```bash
# 1. Init from the official C# template
azd init --template remote-mcp-functions-csharp -e my-mcp-server

# 2. Run locally and smoke-test in VS Code Copilot Chat (agent mode)
func start

# 3. Deploy to Azure
azd up
```
The project template includes a `.vscode/mcp.json` file that already defines a local MCP function server pointing to your local endpoint. Open it and select **Start**, then in the Copilot Chat window make sure **Agent mode** is selected and verify that `MCP Server:local-mcp-function` is enabled in the chat.
For .NET specifically,
the Azure Functions MCP extension supports only the **isolated worker model** for C#, so add the extension to your project by installing the relevant NuGet package.
Also note:
Azure Functions now supports .NET 10 on the isolated worker model across all plan types except Linux Consumption — and support for the legacy in-process model ends on November 10, 2026, with no .NET 10 support for in-process.
If you're still on in-process, now is the time to migrate.

---

## One More Thing: Visual Studio 2026 Already Has Azure MCP Built In

If VS Code isn't your primary IDE,
Azure MCP Server tools are now generally available out-of-the-box in Visual Studio 2026, bringing agentic cloud automation directly into the IDE and empowering developers to build intelligent, secure applications faster, with less complexity.
Visual Studio 2026 also introduces automated CI/CD setup that can generate Azure DevOps or GitHub Actions workflows for ASP.NET, Blazor, or Azure Functions projects — complete with YAML files and securely managed credentials.
Natural language → working pipeline. It's not magic; it's just a very good language model with the right context.

---

## Practical Takeaways

1. **Update AI Toolkit today** — v0.32.0 is available in the VS Code Marketplace; don't wait until June 1st when the Foundry sidebar retires.
2. **Adopt the isolated worker model** for any Azure Functions targeting .NET 10; in-process support ends November 2026.
3. **Use MCP tool approval controls** during demos and code reviews — your reviewers (and future self) will thank you.
4. **Pick the right hosting plan upfront**: Flex Consumption for cost efficiency, Premium with always-ready instances for latency-sensitive production tools.
5. **Register your MCP server in Azure API Center** for organizational discoverability — optional but strongly recommended for teams.

---

## Further Reading

- https://techcommunity.microsoft.com/blog/azuredevcommunityblog/%F0%9F%9A%80-ai-toolkit-for-vs-code-%E2%80%94-march-2026-update/4502517
- https://www.infoq.com/news/2026/01/azure-functions-mcp-support/
- https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-mcp
- https://learn.microsoft.com/en-us/azure/azure-functions/scenario-custom-remote-mcp-server
- https://devblogs.microsoft.com/visualstudio/azure-mcp-server-now-built-in-with-visual-studio-2026-a-new-era-for-agentic-workflows/
- https://techcommunity.microsoft.com/blog/appsonazureblog/building-mcp-apps-with-azure-functions-mcp-extension/4496536
- https://learn.microsoft.com/en-us/azure/foundry/mcp/build-your-own-mcp-server
- https://learn.microsoft.com/en-us/azure/azure-functions/functions-mcp-tutorial
- https://blogs.microsoft.com/blog/2026/03/16/microsoft-at-nvidia-gtc-new-solutions-for-microsoft-foundry-azure-ai-infrastructure-and-physical-ai/