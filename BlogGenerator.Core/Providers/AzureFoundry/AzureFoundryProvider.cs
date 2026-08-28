using System.ClientModel;
using BlogGenerator.Core.Configuration;
using BlogGenerator.Core.Prompts;
using OpenAI;
using OpenAI.Responses;

namespace BlogGenerator.Core.Providers.AzureFoundry;

#pragma warning disable OPENAI001

public sealed class AzureFoundryProvider : IAIProvider
{
    public string ProviderName => "foundry";

    public async Task<AIProviderResponse> GeneratePostAsync(
        PromptContext promptContext,
        GenerationSettings settings,
        CancellationToken ct = default)
    {
        var endpoint = ResolveOpenAiEndpoint();
        var apiKey = ProviderSupport.RequireEnv(
            "FOUNDRY_PROJECT_API_KEY must be set to a non-empty API key.", "FOUNDRY_PROJECT_API_KEY");
        var models = ProviderSupport.ModelCandidates(settings.FoundryDefaultModel, settings.FoundryModels);

        var client = new ResponsesClient(
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions { Endpoint = endpoint });

        Exception? lastErr = null;
        foreach (var candidate in models)
        {
            Console.WriteLine($"Trying Foundry model: {candidate}");

            try
            {
                CreateResponseOptions responseOptions = new()
                {
                    Model = candidate,
                    ToolChoice = ResponseToolChoice.CreateRequiredChoice(),
                    MaxOutputTokenCount = settings.FoundryMaxTokens,
                    MaxToolCallCount = settings.MaxSearches,
                    Temperature = (float?)settings.FoundryTemperature,
                    TopP = (float?)settings.FoundryTopP,
                    InputItems =
                    {
                        ResponseItem.CreateDeveloperMessageItem(promptContext.SystemPrompt),
                        ResponseItem.CreateUserMessageItem(promptContext.UserPrompt),
                    },
                    Tools = { ResponseTool.CreateWebSearchPreviewTool() },
                };

                ResponseResult response = await client.CreateResponseAsync(responseOptions, ct);

                foreach (ResponseItem item in response.OutputItems)
                {
                    if (item is WebSearchCallResponseItem webSearchCall)
                        Console.WriteLine($"Web search invoked: {webSearchCall.Status} ({webSearchCall.Id})");
                }

                var markdown = response.GetOutputText()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(markdown))
                    throw new InvalidOperationException("Foundry response did not contain output text.");

                Console.WriteLine($"Found working model: {candidate}");
                return new AIProviderResponse(markdown, candidate);
            }
            catch (Exception ex)
            {
                lastErr = ex;
                Console.WriteLine($"Foundry call failed for {candidate}: {Redact(ex.Message, endpoint, apiKey)}");
            }
        }

        throw new InvalidOperationException(
            $"No available Foundry deployment found after trying models: [{string.Join(", ", models)}]. Last error: {lastErr?.Message}");
    }

    private static Uri ResolveOpenAiEndpoint()
    {
        var rawEndpoint = ProviderSupport.RequireEnv(
            "FOUNDRY_OPENAI_ENDPOINT must be set to a non-empty Azure OpenAI endpoint.",
            "FOUNDRY_OPENAI_ENDPOINT");

        if (!Uri.TryCreate(rawEndpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("FOUNDRY_OPENAI_ENDPOINT is not a valid absolute URI.");

        // Callers may configure the bare resource host; the Responses API lives under /openai/v1/.
        if (!rawEndpoint.Contains("/openai/", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new Uri(endpoint, endpoint.AbsolutePath.EndsWith('/')
                ? "openai/v1/"
                : $"{endpoint.AbsolutePath.TrimEnd('/')}/openai/v1/");
        }

        return endpoint;
    }

    private static string Redact(string message, Uri endpoint, string apiKey) =>
        ProviderSupport.Redact(
            message,
            (endpoint.ToString(), "FOUNDRY_OPENAI_ENDPOINT"),
            (apiKey, "FOUNDRY_PROJECT_API_KEY"));
}
