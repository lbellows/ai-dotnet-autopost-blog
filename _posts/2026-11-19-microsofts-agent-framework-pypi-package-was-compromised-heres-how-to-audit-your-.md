---
layout: post
title: "Microsoft's Agent Framework PyPI Package Was Compromised — Here's How to Audit Your Supply Chain"
date: 2026-11-19 15:00:25 -0500
tags: [https, package, python, microsoft, muse-spark-open, deepseek-v4-1-flash-1017]
author: the.serf
---

On November 17, 2026, two versions of the Python package `agent-framework` — 1.26.1 and 1.26.2, published 33 minutes apart — carried a credential stealer. The payload was wired through a `.pth` file, which Python executes on interpreter startup, so the malicious code ran whether or not you ever imported the package in your own code. If your build agents, dev containers, or laptops ran `pip install agent-framework` that day, you should assume credentials in those environments were exposed until you prove otherwise.

This is not a Microsoft .NET story, and it is not a Foundry API change. It is a supply-chain incident affecting a package named `agent-framework`, published to PyPI. Microsoft's Agent Framework itself supports Python and .NET; the compromise reported here is specifically the Python distribution on PyPI. Read the rest with that distinction in mind.

## What the malicious versions actually did

According to the November 18 digest covering the incident, the injected payload harvested environment variables whose names contained `TOKEN` or `SECRET`, read them from other running processes as well as its own, and additionally requested Azure access tokens. Exfiltration went to `webhook.site`, a legitimate webhook-testing service that is also a convenient dead-drop for attackers because it requires no infrastructure to stand up.

The `.pth` mechanism is the part that deserves attention. Python processes `.pth` files found in site-packages directories during startup, treating each line as either a path to add or — if it begins with `import` — a statement to execute. That means the stealer had no dependency on your application importing `agent_framework`. It ran when `python` started. A CI job that installed the package and then ran an unrelated script was still in scope. A test runner that shells out to Python was in scope. A container image baked on November 17 is in scope every time it starts.

The 33-minute gap between the two versions suggests a rushed second attempt, possibly to correct a broken payload or extend its reach. Either way, both versions are suspect.

## Why this matters beyond the immediate blast radius

Agent tooling is unusually high-value for a credential stealer, because the environments that run it are unusually well-provisioned. An agent framework typically needs to call models, which means API keys; it often needs to read repositories, which means GitHub tokens; on Azure it frequently runs under managed identity or a service principal, which means the token request path the payload explicitly targeted. The attacker picked a target where the environment variables are the crown jewels rather than an afterthought.

There is also a timing wrinkle worth noting for anyone tracking the broader release calendar: the same day saw OpenAI ship GPT-6.2 Astra, and Microsoft's Foundry retirement of `o1`, `o1-pro`, `o3`, `o3-pro`, `o3-deep-research`, `o3-mini`, and `o4-mini` execute on November 19. A week of loud model news is a good week to slip a quiet package publish past everyone's attention. That is not a coincidence anyone can prove, but it is a pattern worth internalizing.

![Microsoft's Agent Framework PyPI Package Was Compromised — Here's How to Audi...](https://i.imgflip.com/b6aohm.jpg)

## The audit sequence

The practical work splits into three steps, in order of urgency.

**First, find out whether you were exposed.** The question is not "does my project depend on agent-framework" but "did any environment capable of installing it from PyPI resolve to 1.26.1 or 1.26.2." Check lockfiles, container image build logs, and CI cache warm-up steps. A `requirements.txt` that pins a range rather than an exact version will not tell you what actually landed. You need the resolved artifact list from the build that ran on or after November 17.

**Second, rotate everything that environment could reach.** The payload's filters tell you where to start — anything named with `TOKEN` or `SECRET` — but the filters are a lower bound, not a specification. Rotate Azure credentials (client secrets, and for managed identity, review token issuance logs for the affected window), GitHub PATs and app tokens, model provider API keys, and any database or storage connection strings present in those environments. Assume the attacker took everything readable, not only what the filter names suggest.

**Third, pin to a clean release and prove it.** Do not simply upgrade and move on; confirm that the version you land on is one Microsoft has explicitly identified as clean, and pin it exactly. Ranges are what let a compromised publish reach you in the first place.

## What to change in your pipeline

The `.pth` vector is the specific lesson here, and it generalizes past this one package. Any dependency install step that runs Python in an environment holding production credentials is a potential execution surface, because site-packages is executable content by design. Two mitigations are cheap enough to adopt broadly:

- Install dependencies in a build stage that has no secrets mounted, then copy artifacts into a runtime stage that does. If the install step cannot see the token, the stealer has nothing to take.
- Prefer installs from an internal mirror or a hash-pinned lockfile over direct PyPI resolution in CI. A mirror with an approval gate turns a 33-minute window into a non-event.

Neither of these is exotic, and neither requires trusting a vendor's process. They reduce the value of a successful publish compromise rather than trying to prevent it, which is the correct posture for a threat model where the registry is not fully under your control.

## The uncomfortable part

Microsoft's .NET-side SDK packages went through their own churn this cycle — the Foundry SDK removed ambient `TokenCredential` registration and made `FOUNDRY_PROJECT_ENDPOINT` a fallback rather than the primary source, and moved HTTPS enforcement behind `IHostEnvironment.IsDevelopment()`. Those are behavior changes worth reading, but they are ordinary breaking-change hygiene. A credential stealer in a dependency is a different category of problem, and it does not get fixed by a version bump alone.

The honest summary: if you install agent tooling into credentialed environments, you have a supply-chain problem whether or not this particular package was ever on your dependency tree. November 17 is a good day to find out where your gaps are.

## Further reading

https://www.bitdoze.com/news/2026-11-18/
https://github.com/microsoft/agent-framework/releases
https://github.com/Azure/azure-sdk-for-net/releases
https://www.developersdigest.tech/blog/azure-openai-foundry-retirements-november-2026
https://sharedsapience.substack.com/p/the-century-report-november-19-2026
https://www.cnet.com/tech/services-and-software/openai-chatgpt-astra-6-2-microsoft-ignite-news/
https://learn.microsoft.com/en-us/azure/foundry/foundry-models/whats-new-model-router