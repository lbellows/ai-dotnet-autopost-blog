using System.Net.Http.Json;
using System.Text.Json;

namespace BlogGenerator.Core.Providers;

/// <summary>
/// Plumbing every provider needs: resolving a secret from the environment, keeping that secret
/// out of log lines, ordering the model fallback list, and posting a JSON body with a useful
/// error when the API rejects it.
/// </summary>
internal static class ProviderSupport
{
    /// <summary>
    /// Returns the first non-empty environment variable among <paramref name="names"/>, or throws
    /// with <paramref name="errorMessage"/> when none is set.
    /// </summary>
    public static string RequireEnv(string errorMessage, params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    /// Replaces secrets with their placeholder names so failed-request messages stay loggable.
    /// </summary>
    public static string Redact(string message, params (string Value, string Placeholder)[] secrets)
    {
        foreach (var (value, placeholder) in secrets)
        {
            if (!string.IsNullOrEmpty(value))
                message = message.Replace(value, $"[{placeholder}]", StringComparison.OrdinalIgnoreCase);
        }

        return message;
    }

    /// <summary>
    /// The ordered, de-duplicated model list to try: the primary model first, then its fallbacks.
    /// </summary>
    public static IReadOnlyList<string> ModelCandidates(string? primary, IEnumerable<string> fallbacks)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(primary))
            candidates.Add(primary);
        candidates.AddRange(fallbacks);

        return candidates
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// POSTs <paramref name="body"/> as JSON and returns the raw response body, throwing with the
    /// API's own error text when the call fails — that text is usually the only clue to why.
    /// </summary>
    public static async Task<string> PostJsonAsync<TBody>(
        HttpClient http,
        string url,
        TBody body,
        JsonSerializerOptions jsonOptions,
        string providerName,
        Action<HttpRequestMessage> configureRequest,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        configureRequest(request);
        request.Content = JsonContent.Create(body, options: jsonOptions);

        var response = await http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"{providerName} request failed with {(int)response.StatusCode} ({response.ReasonPhrase}). Response body: {responseBody}",
                null,
                response.StatusCode);
        }

        return responseBody;
    }
}
