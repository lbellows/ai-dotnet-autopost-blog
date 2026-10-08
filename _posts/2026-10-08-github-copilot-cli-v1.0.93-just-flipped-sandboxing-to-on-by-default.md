---
layout: post
title: "GitHub Copilot CLI v1.0.93 Just Flipped Sandboxing to On-by-Default"
date: 2026-10-08 14:24:44 -0400
tags: [github, agent, copilot, october, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

Something quiet and important happened to agentic coding tools on October 7, 2026: GitHub Copilot CLI shipped v1.0.93, and the two headline changes are exactly the kind that alter how you operate the thing rather than how it feels. First, real-time sandboxing is now on for all users — not an opt-in flag you remember to set on a good day. Second, enterprise permission controls got sharper, which matters the moment an org tries to reason about what the agent is allowed to touch.

If you've been treating your CLI agent as "just a shell with a nicer prompt," this release is the nudge to stop. The default changed, and defaults are the only security control people actually keep.

## What actually shipped

According to the Copilot CLI changelog, v1.0.93 (October 7, 2026) brings "smarter command handling with real-time sandboxing for all users and improved permission controls for enterprise environments." The predecessor, v1.0.91 on October 1, added sandbox commands on Windows plus certificate-related security controls. So this isn't a lone spike — it's a two-release arc toward constraining what agent-initiated tools and commands can do.

That distinction matters for engineers because sandboxing in an agentic CLI isn't a performance feature. It's a policy boundary over filesystem, network, and credentials. When that boundary is applied by default, your existing scripts and CI jobs may behave differently on upgrade even though you changed nothing.

## The enterprise permission controls are the real headline

Sandboxing gets the attention; permission controls decide whether you can deploy the tool. In an enterprise setting, the question isn't "can the agent run commands" — it's "under what authority, with what audit trail, and who can loosen the leash." Improved org-level permission controls are the difference between a security review that takes an afternoon and one that takes a quarter.

A useful way to think about the layering:

- **Sandbox** = what the process can reach at runtime (filesystem, network, credentials).
- **Permissions** = whether the agent is allowed to attempt the action at all, and under whose policy.
- **Audit/metrics** = whether you can prove afterward what happened.

That third layer is currently messier than the first two. GitHub has acknowledged a usage-metrics gap: IDEs that moved Copilot agent sessions onto the Copilot SDK stopped reporting the originating IDE, so some agent activity was dropped or miscounted as Copilot CLI. The fix landed in VS Code 1.139.0 and later, with Visual Studio, JetBrains, Eclipse, and Xcode fixes expected by November 2026. Billing itself is unchanged, and — this is the painful part — missing data cannot be backfilled.

![GitHub Copilot CLI v1.0.93 Just Flipped Sandboxing to On-by-Default meme](https://i.imgflip.com/b30s0z.jpg)

If you run chargebacks or per-team AI budgets, plan for that hole. Don't rebuild historical numbers from a source that admitted it under-counted.

## Why "on by default" beats "available"

Engineers have a well-documented relationship with optional security flags: we enable them in the environment where we're being watched and forget them everywhere else. A sandbox that ships off is a sandbox that ships never. Making real-time sandboxing the default for all users collapses the gap between the security posture you documented and the one your laptop actually runs.

The tradeoff is real, though. Defaults that restrict filesystem and network reach will occasionally break a workflow that relied on implicit access — say, a tool that shells out to a service on localhost, or a script that reads a credential file outside the project tree. Expect a short tail of "worked yesterday" reports. The fix is to declare the capability in policy, not to disable the boundary.

## How this fits the rest of the GitHub October batch

The same October changelog window brought local sandboxing to general availability across Copilot CLI, the Copilot app, and the VS Code Agent Host, with policy-driven boundaries over filesystem, network, credentials, and other system capabilities. GitHub also added `/security-review` for inspecting active changes and returning prioritized findings, plus a built-in security-review specialist for coding agents — new opt-in push-protection checks consume AI Credits under the Secret Protection SKU once an org opts into public preview.

There's an adjacent piece of plumbing worth putting on the calendar even though it isn't an AI feature: newly minted GitHub App installation tokens now use a stateless `ghs_APPID_JWT` format at roughly 520 characters instead of 40. Permissions, repo scoping, the one-hour expiration, and the installation-access-token REST endpoint are unchanged. The temporary `X-GitHub-Stateless-S2S-Token` header is deprecated on November 30, 2026.

The lesson generalizes beyond GitHub: if your integration asserts `token.Length == 40` anywhere, it will fail in a way that looks like an auth problem and isn't. Treat tokens as opaque strings. It costs nothing and saves an incident review.

## Practical moves for the next two weeks

1. **Pin your CLI version in CI.** A default-security change is a behavioral change. Let the upgrade be a deliberate commit, not a Tuesday surprise.
2. **Inventory what your agent workflows reach.** Enumerate the hosts, files, and credentials they touch. Anything not on that list is now a candidate for a policy exception rather than a silent dependency.
3. **Decide your permission model before the security review, not during it.** Org policy is easier to defend when it's written down.
4. **Annotate your metrics dashboards.** Note the Copilot SDK attribution gap and its fix timeline so nobody misreads a dip as adoption decline.
5. **Grep for length assumptions on tokens.** Forty characters is no longer a safe bet.

None of this is glamorous. But the difference between an agent you can run in a regulated environment and one you can't is usually a handful of defaults like this one — and as of October 7, 2026, GitHub moved one of them in the right direction.

## Further reading

- https://www.havoptic.com/tools/github-copilot
- https://releasebot.io/updates/github
- https://releases.sh/github
- https://developer.microsoft.com/en-us/changelog
- https://kimbodo.com/ai-coding-developer-tools-september-30-2026/
- https://github.com/Chestnuts-Sisyphus/gittok/issues/1608
- https://releasebot.io/updates/microsoft