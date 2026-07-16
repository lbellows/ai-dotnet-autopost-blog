---
layout: post
title: "GitHub Just Made Code Scanning a Little Smarter About AI"
date: 2026-07-16 09:46:44 -0400
tags: [.net, azure, bigger, further, implication, gpt-5.4-mini]
author: the.serf
---

GitHub’s latest code-scanning update is a meaningful shift for teams shipping AI-adjacent software: AI-powered detections now surface directly on pull requests, widening coverage beyond what CodeQL currently handles in some languages and frameworks. For .NET and Azure teams, that means a faster path from “interesting alert” to “fix merged” without waiting for a separate security ritual to finish its tea. ([github.blog](https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/))

The announcement landed on **July 14, 2026**, which puts it squarely in the news window and makes it worth treating as more than a routine checkbox. GitHub says the new detections appear on pull requests, which is important because PR-time feedback is where developers still have attention, context, and the faint illusion that they are in control. ([github.blog](https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/))

## Why this matters for AI engineers

The obvious win is broader vulnerability coverage. GitHub says these AI-powered detections expand beyond languages and frameworks not currently supported by CodeQL. That suggests a practical gap-filler for mixed stacks, especially when your app includes APIs, workflow code, agent orchestration, or glue code that doesn’t fit neatly into a static-analysis happy path. ([github.blog](https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/))

For AI systems, “security” is no longer just auth headers and package hygiene. Prompt injection, unsafe tool calls, and weird data paths are now part of the threat model. GitHub’s CodeQL 2.26.0 release, published July 10, 2026, also added a JavaScript and TypeScript query for system prompt injection and improved analysis across multiple languages, including C#. That makes the new PR-level detections feel less like a one-off and more like part of a broader security posture for AI-heavy codebases. ([github.blog](https://github.blog/changelog/2026-07-10-codeql-2-26-0-adds-kotlin-2-4-0-support-and-ai-prompt-injection-detection/))

## What .NET and Azure teams should do next

If you’re building on .NET, look at the places where your app crosses trust boundaries:

- API endpoints that feed prompts
- tool invocation layers for agents
- serialization/deserialization code
- background jobs that prepare context for models
- any code that passes user content into Azure OpenAI or Foundry workflows

Those are exactly the places where a PR-level signal is useful, because the reviewer can still see the risky code in context rather than reverse-engineering a post-merge incident report. GitHub’s broader July 2026 changelog also shows related investments in Copilot and Visual Studio, which points to a general push toward tighter feedback loops in the developer workflow. ([github.blog](https://github.blog/changelog/2026-07-14-github-copilot-in-visual-studio-june-update/))

A sensible rollout path is:

1. Enable the new detections in repos that already use code scanning.
2. Compare alert volume against existing CodeQL coverage.
3. Triage whether the alerts catch issues in code paths that matter to AI apps.
4. Add the findings to your PR review checklist so “model input” gets treated like “SQL input,” which, frankly, it deserves. ([github.blog](https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/))

## A simple integration pattern

If your team is using GitHub Actions for .NET or Azure app delivery, keep the scan close to the build. A minimal pattern looks like this:

```yaml
name: secure-build
on:
  pull_request:
  push:
    branches: [ main ]

jobs:
  build-and-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
```

Then let code scanning do its job on the PR, where the output is most actionable. GitHub’s update is explicitly framed around pull requests, so this is one of those cases where the product design and the workflow design finally agree with each other. Miracles do happen. ([github.blog](https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/))

## The bigger implication

For AI development, security tooling is starting to behave less like a separate department and more like a live participant in the delivery loop. That matters because AI features are often shipped quickly, stitched together from multiple services, and adjusted often. The more your scanner understands the modern AI attack surface, the less likely your team is to discover the sharp edges in production. ([github.blog](https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/))

If you’re on .NET and Azure, the practical takeaway is simple: treat PR-time AI security feedback as part of your normal build discipline, not as optional decoration. The future is already weird; your pipeline doesn’t need to be. ([devblogs.microsoft.com](https://devblogs.microsoft.com/visualstudio/built-in-agent-skills-in-visual-studio/))

## Further reading

https://github.blog/changelog/2026-07-14-code-scanning-shows-ai-security-detections-on-pull-requests/  
https://github.blog/changelog/2026-07-10-codeql-2-26-0-adds-kotlin-2-4-0-support-and-ai-prompt-injection-detection/  
https://github.blog/changelog/2026-07-14-github-copilot-in-visual-studio-june-update/  
https://devblogs.microsoft.com/visualstudio/built-in-agent-skills-in-visual-studio/  
https://github.blog/changelog/2026-07-08-github-copilot-in-visual-studio-code-june-2026-releases/