---
layout: post
title: "`@modernize-dotnet` Is Everywhere Now: What the New GitHub Copilot Agent Means for Your .NET Stack"
date: 2026-03-14 07:38:56 -0400
tags: [azure, workflow, .net, actually, add, claude-sonnet-4-6]
author: the.serf
---

**TL;DR:** As of mid-March 2026, the `modernize-dotnet` GitHub Copilot agent is no longer just a Visual Studio feature. It now runs inside VS Code, the GitHub Copilot CLI, and directly on GitHub.com — following a structured assess → plan → execute workflow. If you have legacy .NET Framework or older .NET projects you've been putting off upgrading (we all have that one service running on .NET 6 in the corner), this dramatically lowers the barrier to starting.

---

## The Problem It Solves (Honest Edition)

Upgrading a .NET project sounds like it should be a weekend task. In practice, it's a multi-day archaeology expedition through NuGet package conflicts, deprecated APIs, and CI pipelines that someone wrote three years ago and nobody fully understands anymore. The old `.NET Upgrade Assistant` was helpful but deterministic — it did what it knew how to do and stopped. The new AI-backed agent is designed to handle the long tail of edge cases, learn from your manual corrections mid-session, and surface every breaking change before a single line of code is touched.

---

## What's Actually New This Week
Until recently, GitHub Copilot modernization for .NET ran primarily inside Visual Studio. That worked well for teams standardized on the IDE, but many teams build elsewhere — some use VS Code, some work directly from the terminal, and much of the coordination happens on GitHub, not in a single developer's local environment.
The `modernize-dotnet` custom agent changes that. The same modernization workflow can now run across Visual Studio, VS Code, GitHub Copilot CLI, and GitHub — the intelligence behind the experience remains the same; what's new is *where* it can run. You can modernize in the environment you already use instead of rerouting your workflow just to perform an upgrade.
This isn't a minor quality-of-life bump. For distributed teams where half the engineers are terminal-first and the other half live in VS Code, the old "open Visual Studio to run a modernization" requirement was a genuine friction point.

---

## The Three-Stage Workflow
When you ask the modernization agent to upgrade your app, Copilot runs a three-stage workflow, and each stage writes a Markdown file under `.github/upgrades` in your repository so you can review what comes next before you continue.
The stages map cleanly to how a thoughtful senior engineer would approach the same task:

1. **Assessment** —
Copilot examines your project structure, dependencies, and code patterns to build a comprehensive assessment; the document lists breaking changes, API compatibility problems, deprecated patterns, and the upgrade scope so you know exactly what needs attention.
2. **Planning** —
Copilot creates a detailed specification explaining how to resolve every problem.
3. **Execution** —
Copilot breaks the plan into sequential tasks and performs the upgrade.
Crucially,
the tool creates a Git commit for every portion of the process, so you can easily roll back changes or get detailed information about what changed.
The Markdown artifacts are editable, which matters:
you can edit any of the Markdown files in `.github/upgrades` to adjust upgrade steps or add context before you move forward, and after each stage completes, review and modify the generated tasks as needed before telling Copilot to continue.
---

## Using It from the CLI (The Terminal-First Workflow)

For engineers who prefer to stay in the shell, the CLI path is now first-class:

```bash
# Add the marketplace
/plugin marketplace add dotnet/modernize-dotnet

# Install the plugin
/plugin install modernize-dotnet@modernize-dotnet-plugins

# Select the agent
/agent to select modernize-dotnet

# Kick off an upgrade
upgrade my solution to a new version of .NET
```
You can assess a repository, generate an upgrade plan, and run the upgrade without leaving the shell. The agent generates the assessment, upgrade plan, and upgrade tasks directly in the repository. You can review scope, validate sequencing, and approve transformations before execution — once approved, the agent automatically executes the upgrade tasks directly from the CLI.
---

## Using It from VS Code
If you use VS Code, install the GitHub Copilot modernization extension and select `modernize-dotnet` from the Agent picker in Copilot Chat. Then prompt the agent with the upgrade you want to perform — for example: `upgrade my project to .NET 10`.
---

## Project Types Supported
GitHub Copilot modernization supports upgrades across common .NET project types, including ASP.NET Core (MVC, Razor Pages, Web API), Blazor, Azure Functions, WPF, class libraries, and console applications.
Azure Functions is on that list, which is particularly relevant given the broader push toward AI-enabled serverless workloads on Azure right now.

---

## The Broader Context: Azure Summit + Modernization Momentum

This agent release didn't land in isolation.
A livestream event on March 12, 2026 (Asia and Europe) — the Microsoft Azure Summit: Migrate and Modernize with Agentic AI — featured interactive sessions led by Microsoft engineering with compelling customer stories and live demos of the latest innovations.
Microsoft announced the first agentic end-to-end modernization solution that brings IT and developers into a single, connected workflow — with expanded capabilities across Azure Copilot and GitHub Copilot, AI is being infused through these tools to accelerate insight-driven decisions and move modernization from analysis to action. AI agents make this possible by operating in parallel across discovery, assessment, planning, migration, and code transformation, automating dependency mapping, generating decision-ready plans, and guiding execution inside the tools teams already use.
Translation: Microsoft is betting that the biggest unlock for legacy .NET shops isn't a new framework feature — it's removing the human coordination overhead that makes upgrades feel expensive.

---

## Important Caveats (Don't Skip This Section)

Community feedback on the agent has been mixed for complex projects. Some engineers have noted that for simple framework bumps (e.g., .NET 9 → .NET 10), the old `.NET Upgrade Assistant` is still faster and more deterministic.
The upgrade or migration suggestions aren't guaranteed to follow best practices.
The agent is designed for *complex* multi-project solutions where it genuinely earns its keep — straightforward projects may not need the overhead.

Also worth noting on data privacy:
the agent never stores your codebase or uses your code to train the model, and once an upgrade or migration completes, the agent deletes session data.
---

## Prerequisites at a Glance

| Environment | Minimum Requirement |
|---|---|
| Visual Studio | VS 2026 or VS 2022 v17.14.17+ |
| VS Code | GitHub Copilot modernization extension |
| GitHub Copilot CLI | CLI installed + Copilot subscription |
| GitHub.com | Copilot Enterprise or Business with coding agents enabled |
For Visual Studio, you need Visual Studio 2026 (or VS 2022 version 17.14.17+), the .NET desktop development workload with the GitHub Copilot and GitHub Copilot app modernization optional components enabled, and a GitHub Copilot subscription (paid or free).
---

## Practical Takeaways for .NET + Azure Teams

- **Start with your most painful legacy project**, not your cleanest one. The agent's value is proportional to the complexity of the migration.
- **Review every Markdown artifact before saying "continue."** The plan and task files are your safety net — treat them like a PR review, not a formality.
- **Pin the agent in your CI-adjacent workflow.** Because it integrates with GitHub.com directly, you can treat modernization as a tracked, reviewable team effort rather than a single developer's local experiment.
- **Don't cancel your `.NET Upgrade Assistant` muscle memory** for simple, one-project upgrades. Use the right tool for the job.
- **If you're on Azure Functions**, this is especially timely:
Microsoft has also moved its Model Context Protocol (MCP) support for Azure Functions to General Availability
, meaning the platform you're upgrading *to* is more capable than ever for AI-native workloads.

---

## Further Reading

- https://devblogs.microsoft.com/dotnet/modernize-dotnet-anywhere-with-ghcp/
- https://learn.microsoft.com/en-us/dotnet/core/porting/github-copilot-app-modernization/overview
- https://azure.microsoft.com/en-us/blog/many-agents-one-team-scaling-modernization-on-azure/
- https://learn.microsoft.com/en-us/dotnet/core/porting/github-copilot-app-modernization/how-to-upgrade-with-github-copilot
- https://www.infoq.com/news/2026/01/azure-functions-mcp-support/
- https://learn.microsoft.com/en-us/azure/foundry/openai/concepts/model-retirements?view=foundry-classic