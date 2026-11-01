---
layout: post
title: "Azure AI Projects Shipped Three SDKs on October 30 — and None of Them Were .NET"
date: 2026-11-01 12:45:31 -0500
tags: [.net, azure, python, sdks, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

On October 30, 2026, the Azure AI Projects SDK got coordinated v2 releases across Python, JavaScript/TypeScript, and Java. Python and JS both landed on 2.10.0; Java landed on 2.9.0. The tracking issue describes it as a "coordinated service-contract update," which is the polite way of saying the service moved and the clients followed.

What did not move: .NET. The monitored v2 line for Azure AI Projects still sits at 2.1.0-beta.4. If you're building Foundry agents on the .NET SDK, the service contract you're coding against is now at least one coordinated update behind the other three languages — and it's still a preview.

## The version spread, plainly

Here's what actually changed on 2026-10-30, per the tracking issue:

| Language | Version | Status |
|---|---|---|
| Python | 2.10.0 | Shipped |
| JavaScript/TypeScript | 2.10.0 | Shipped |
| Java | 2.9.0 | Shipped |
| .NET | 2.1.0-beta.4 | Unchanged (preview) |

The Java version number being lower than Python and JS is worth noting but not alarming — these SDKs don't version in lockstep across languages even when they ship the same day. The "coordinated" part is about the service contract, not the semver.

## Why a coordinated bump matters more than a single bump

When three SDKs release on the same day against the same service, it usually means one of two things: either a new capability landed and each team wired it up independently, or a contract changed and each team had to adjust. Either way, the .NET client is now the odd one out.

For engineers running polyglot Foundry workloads — a Python scoring service, a JS agent orchestrator, a Java batch processor, and a .NET API layer — this is the moment where you start seeing behavioral drift. The Python service calls a field that the .NET client doesn't know about yet. The JS orchestrator sets a configuration the .NET layer silently ignores. Nothing throws. Things just quietly diverge.

The pragmatic move is to check whether your integration surface touches anything the new v2 contract changed. If your .NET code only reads and writes stable fields, you're probably fine. If you're relying on newer service features, you're the person who finds out first.

## The wider picture: it's a busy week for SDKs that aren't yours

The same 2026-10-30 window saw a few other things worth noting, none of which are .NET SDK ships:

- **Claude Code v2.1.310 and v2.1.311** added a Monitor tool and plugin monitors on Bedrock, Vertex, and Foundry. That last one is the interesting bit — observability into managed deployments on Azure AI Foundry is a genuinely useful addition, and it's new ground relative to the Foundry catalog and data-zone work that's been covered elsewhere.
- **OpenAI's Decisions API reached GA** with gpt-6-luna, dated 2026-10-30. The snippet frames it as prompt-spend news, which tells you the interesting part is cost, not capability. Luna list price sits at $0.10 input / $0.50 output per million tokens on the standard synchronous cache-miss rate.
- **GitHub Copilot CLI** had no new releases or merged PRs on October 30, per the AI Ecosystem Digest. The v1.0.93 sandboxing behavior from earlier in October remains the current state.

## What to do Monday morning

If you own .NET code against Azure AI Projects, the checklist is short:

1. **Pin your SDK version explicitly.** If you're floating a preview, you're already exposed. Pin it.
2. **Diff your integration surface against the v2 contract.** The tracking issue is the fastest way to see what the other three languages got.
3. **Check your polyglot boundaries.** If a Python or JS service in your stack talks to a .NET service and both touch Azure AI Projects, verify the field names and config keys still line up.
4. **Don't wait for a .NET 2.10.0.** The v2 line has been sitting at beta.4 while the others moved. Plan around it, not for it.

The .NET SDK will get there. It always does. But "it always does" is not a deployment strategy, and a coordinated three-language release is about as loud a signal as you get that the gap is real.

![Azure AI Projects Shipped Three SDKs on October 30 — and None of Them Were .N...](https://i.imgflip.com/b4ts8y.jpg)

## Further reading

- https://github.com/nicholasdbrady/foundry-docs/issues/1237
- https://github.com/microsoft/agent-framework/releases
- https://releases.sh/github
- https://finopsweekly.com/news/ai-economics-provider-updates-2026-10-30/
- https://developers.openai.com/api/docs/changelog
- https://github.com/datnguyenquy94/news-radar/issues/934
- https://devblogs.microsoft.com/azure-sdk/azure-sdk-release-october-2026/
- https://azure.github.io/azure-sdk/releases/latest/
- https://github.com/Azure/azure-sdk/pull/10341
- https://learn.microsoft.com/en-us/microsoft-365/copilot/release-notes