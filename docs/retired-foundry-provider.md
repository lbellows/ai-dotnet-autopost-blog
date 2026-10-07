# Retired: the Azure Foundry provider

Removed 2026-10-07. The last commit that still has it is `d30a966`; check that out to bring it
back (`git show d30a966:BlogGenerator.Core/Providers/AzureFoundry/AzureFoundryProvider.cs`).

## Why it went

It wrote most of the blog from March to August 2026 (about 120 posts, tagged `gpt-5.4-mini`), but
nothing since. The scheduled run moved to Venice in September, and Anthropic stayed as the direct
hosted option. Keeping Foundry meant the `OpenAI` NuGet package (used by nothing else), an
`OPENAI001` warning suppression for the preview Responses API, two secrets, five settings and a
workflow branch, for a path nobody ran.

## What it did

`dotnet run --project BlogGenerator -- foundry` (alias `azure`) wrote one post in a single
request to Azure OpenAI's **Responses API**, with Azure's own web search doing the grounding. It
was the same "one call searches and writes" shape as the Anthropic provider.

- **Endpoint and auth.** `FOUNDRY_OPENAI_ENDPOINT` (an `https://<resource>.openai.azure.com/`
  URL) and `FOUNDRY_PROJECT_API_KEY`, both from the environment, API-key auth only. A bare
  resource host was accepted: if the URL had no `/openai/` segment, `/openai/v1/` was appended,
  because that is where the Responses API lives.
- **Client.** `OpenAI.Responses.ResponsesClient` from the `OpenAI` package (2.14.0), built with an
  `ApiKeyCredential` and `ResponsesClientOptions { Endpoint = ... }`. No `Azure.AI.Projects` and
  no Entra ID; an earlier version used that SDK and was replaced on 2026-03-14.
- **Request.** One `CreateResponseOptions` per attempt:
  - `InputItems`: the system prompt from `PromptBuilder.Build` as a **developer** message, then
    the user prompt.
  - `Tools`: `ResponseTool.CreateWebSearchPreviewTool()`, and
    `ToolChoice = ResponseToolChoice.CreateRequiredChoice()` so the model had to search before
    writing.
  - `MaxToolCallCount = MaxSearches`, `MaxOutputTokenCount = FoundryMaxTokens`, plus optional
    `Temperature` / `TopP`.
- **Response.** `response.GetOutputText()` was the post. Each `WebSearchCallResponseItem` in
  `OutputItems` was logged with its status, so the run log showed whether search really ran.
- **Model fallback.** It tried `FoundryDefaultModel` first, then each entry in `FoundryModels`,
  de-duplicated, and moved to the next deployment on any exception or empty output. Errors were
  logged with the endpoint and key redacted. The post was tagged with the deployment that
  succeeded.

## Settings it used

| Setting | Last value | Meaning |
|---|---|---|
| `FoundryDefaultModel` | `gpt-5.4-mini` | Deployment tried first |
| `FoundryModels` | `["gpt-5.4-mini"]` | Fallback deployments, in order |
| `FoundryMaxTokens` | `4096` | Output token cap |
| `FoundryTemperature` | `null` | Sampling temperature (server default when null) |
| `FoundryTopP` | `null` | Nucleus sampling (server default when null) |

Shared settings it also read: `MaxSearches`, `AllowedDomains` and `BlockedDomains`. The domain
lists only reached the model through the prompt text; the web-search tool was created with no
domain filter.

## Lessons worth keeping

- **Pick deployments that support tool calling.** `DeepSeek-V3.2` was dropped from the list
  because Azure documents it without tool calling, and the required web-search choice fails on it.
- **Required tool choice matters.** Without `CreateRequiredChoice()` the model could answer from
  memory and skip search entirely. That is the failure the grounding exists to prevent.
- **The workflow needs both secrets.** It used to validate `FOUNDRY_OPENAI_ENDPOINT` and
  `FOUNDRY_PROJECT_API_KEY` before running, so a missing secret failed in seconds and not halfway
  through generation.
