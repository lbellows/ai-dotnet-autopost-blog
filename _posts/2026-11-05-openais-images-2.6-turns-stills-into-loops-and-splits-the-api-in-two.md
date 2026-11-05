---
layout: post
title: "OpenAI's Images 2.6 Turns Stills Into Loops — And Splits the API in Two"
date: 2026-11-05 14:36:29 -0500
tags: [two, api, flare, .net, grok-4-6, deepseek-v4-1-flash]
author: the.serf
---

OpenAI shipped Images 2.6 on November 4, 2026, and the headline feature is the one your product manager will ask about first: ChatGPT can now generate a looping GIF from a prompt or a photo, with playback-speed control, frame stepping, and per-frame fixes. That's a pleasant toy. The part that matters if you're building software is quieter — the API grew two new image models, `GPT-Image-2.6 Flare` and `GPT-Image-2.6 Sunburst`, and a new parameter that decides how much the model plans before it draws. If you've been wiring image generation into a .NET service or an Azure-hosted pipeline, the model IDs you're calling are now stale, and the new knob is not the quality dial you might assume it is.

Worth saying plainly up front: the launch coverage describes ChatGPT and the OpenAI API. It does not confirm availability in Azure AI Foundry or GitHub. So if you're a Foundry caller, don't assume these model IDs resolve on your endpoint just because they exist upstream.

## Two models, not one model with a mood

The temptation is to read Flare and Sunburst as the same weights with different defaults. They aren't described that way. Flare is built to return quickly and, per OpenAI as quoted in the launch write-up, improves layout quality for posters and product imagery. The two API models are priced identically, so the choice isn't a cost decision — it's a latency-and-composition decision. If you're rendering a hero image for a storefront and the request sits in a user-facing path, Flare's the one you reach for. If you're compositing something with several elements that all have to survive together, you'll want to test Sunburst against your actual layouts rather than trusting a tier name.

That's the kind of distinction that's easy to get wrong in a config file and expensive to discover in production.

## The planning knob is not a quality slider

The genuinely interesting new surface is a low / medium / high control that governs planning effort. It reads like a quality dial. It isn't. The developer guidance attached to the launch is to start low for ordinary edits and raise it only when several elements must survive together. In other words, it's a knob you turn when composition complexity demands it, not one you max out because you assume high means better.

This matters for anyone running image generation at volume. If you default everything to high because it sounds safer, you're paying latency for planning that an ordinary crop-and-adjust request never needed.

![OpenAI's Images 2.6 Turns Stills Into Loops — And Splits the API in Two meme](https://i.imgflip.com/b56mk0.jpg)

## What a .NET caller should actually change

The concrete migration is small, which is the good news. What you're doing is swapping an image model ID and deciding whether to set the planning parameter at the call site. Here's the shape of it, using the Azure OpenAI-style client surface:

```csharp
using Azure.AI.OpenAI;
using OpenAI.Images;
using System.ClientModel;

var client = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")!),
    new DefaultAzureCredential());

ImageClient images = client.GetImageClient("GPT-Image-2.6-Flare");

try
{
    ClientResult<GeneratedImage> result = await images.GenerateImageAsync(
        prompt: "A looping mascot waving, flat vector style",
        options: new ImageGenerationOptions
        {
            Size = GeneratedImageSize.W1024xH1024,
            // Start low for ordinary edits; raise only when multiple
            // elements must survive the edit together.
            AdditionalProperties = { ["planning_effort"] = "low" }
        });

    Console.WriteLine(result.Value.ImageUri);
}
catch (ClientResultException ex) when (ex.Status == 404)
{
    // Model ID not deployed on this endpoint — Flare/Sunburst are
    // not confirmed in Foundry, so fall back to your current model.
    Console.Error.WriteLine($"Image model unavailable: {ex.Message}");
}
```

The 404 path is the one to take seriously. Because the launch coverage stops at ChatGPT and the OpenAI API, a Foundry endpoint may simply not know these names yet. Treating a missing deployment as a hard failure instead of a fallback is how you get a pager alert at 2 a.m. for a model that was never going to be there.

## The naming trap nobody will warn you about

There are now at least three image-model families floating around with maddeningly similar names: `gpt-image-2` (which gained transparent backgrounds in preview), `GPT-Image-2.6`, and the Flare/Sunburst split. These are not the same thing, and a copy-paste from a blog post into a config file is exactly how you end up debugging a 404 that has nothing to do with your credentials.

Copy the model ID from the vendor page that actually serves your endpoint. Names move faster than documentation does.

## The broader pattern worth noticing

Image generation is following the same path text models took: a single "good" model becomes a tiered line, and the tier names stop being self-explanatory. Images 2.0 arrived in April 2026, split into two tiers with 2.5 in September, and now 2.6 adds a planning parameter on top. Meanwhile Microsoft shipped MAI-Image-2.6 back in August, so if you're comparing vendors you've got genuinely different model families with overlapping version numbers. Delightful.

For engineers, the practical consequence is that image generation is no longer a fire-and-forget API call you set once and forget. It now has the two properties that make text-model integration annoying: model IDs that churn, and parameters whose names don't match their behavior. Budget for both.

If you take one thing from Images 2.6, make it this: the GIF is the demo, but the planning parameter and the Flare/Sunburst split are the integration surface. Decide your latency posture deliberately, don't assume the new models exist on your endpoint, and pin the exact model ID you tested rather than the one a roundup gave you.

## Further reading

- https://www.orcarouter.ai/blog/openai-images-2-6-launch-gifs-and-api-models
- https://developers.openai.com/api/docs/changelog
- https://models.litellm.ai/providers/azure_ai
- https://platform.claude.com/docs/en/release-notes/overview
- https://github.com/Azure/azure-sdk-for-net/releases
- https://azure.github.io/azure-sdk/releases/latest/dotnet.html
- https://www.clone-systems.com/blog/new-ai-models-november-2026-release-guide-and-security
- https://convika.com/blog/ai-model-retirements-november-2026-february-2027/