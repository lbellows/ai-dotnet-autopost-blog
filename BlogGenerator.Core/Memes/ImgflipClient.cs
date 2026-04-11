using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlogGenerator.Core.Memes;

public sealed class ImgflipClient(HttpClient http)
{
    private const string GetMemesUrl = "https://api.imgflip.com/get_memes";
    private const string CaptionImageUrl = "https://api.imgflip.com/caption_image";

    // Fetches the top 100 templates and returns them keyed by lower-cased name.
    public async Task<IReadOnlyDictionary<string, ImgflipTemplate>> GetTemplatesAsync(
        CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ImgflipGetMemesResponse>(GetMemesUrl, ct)
            ?? throw new InvalidOperationException("Imgflip get_memes returned null.");

        if (!response.Success)
            throw new InvalidOperationException($"Imgflip get_memes failed: {response.ErrorMessage}");

        return response.Data.Memes
            .ToDictionary(t => t.Name.ToLowerInvariant(), t => t);
    }

    // Finds the best-matching template for the hint name and renders the caption via Imgflip.
    // Returns the hosted image URL on success, or null if no match or the call fails.
    public async Task<string?> CaptionAsync(
        ImgflipHint hint,
        IReadOnlyDictionary<string, ImgflipTemplate> templates,
        CancellationToken ct = default)
    {
        var template = FindTemplate(hint.TemplateName, templates);
        if (template is null)
        {
            Console.WriteLine($"Imgflip: no template matched '{hint.TemplateName}'; skipping meme.");
            return null;
        }

        var formData = new FormUrlEncodedContent(
        [
            new("template_id", template.Id),
            new("text0", hint.TopText),
            new("text1", hint.BottomText),
        ]);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await http.PostAsync(CaptionImageUrl, formData, ct);
            httpResponse.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Imgflip caption_image HTTP error: {ex.Message}");
            return null;
        }

        ImgflipCaptionResponse? result;
        try
        {
            result = await httpResponse.Content.ReadFromJsonAsync<ImgflipCaptionResponse>(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Imgflip caption_image parse error: {ex.Message}");
            return null;
        }

        if (result is null || !result.Success)
        {
            Console.WriteLine($"Imgflip caption_image failed: {result?.ErrorMessage ?? "unknown error"}");
            return null;
        }

        Console.WriteLine($"Imgflip meme generated: {result.Data.Url}");
        return result.Data.Url;
    }

    // Fuzzy match: exact name first, then prefix, then substring.
    internal static ImgflipTemplate? FindTemplate(
        string name,
        IReadOnlyDictionary<string, ImgflipTemplate> templates)
    {
        var key = name.ToLowerInvariant();

        if (templates.TryGetValue(key, out var exact))
            return exact;

        var prefix = templates.Keys.FirstOrDefault(k => k.StartsWith(key, StringComparison.OrdinalIgnoreCase));
        if (prefix is not null) return templates[prefix];

        var substring = templates.Keys.FirstOrDefault(k => k.Contains(key, StringComparison.OrdinalIgnoreCase));
        if (substring is not null) return templates[substring];

        return null;
    }
}

// ── JSON shape ──────────────────────────────────────────────────────────────

public sealed class ImgflipTemplate
{
    [JsonPropertyName("id")]   public string Id   { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("url")]  public string Url  { get; init; } = string.Empty;
}

file sealed class ImgflipGetMemesResponse
{
    [JsonPropertyName("success")]       public bool   Success      { get; init; }
    [JsonPropertyName("error_message")] public string ErrorMessage { get; init; } = string.Empty;
    [JsonPropertyName("data")]          public ImgflipMemesData Data { get; init; } = new();
}

file sealed class ImgflipMemesData
{
    [JsonPropertyName("memes")] public List<ImgflipTemplate> Memes { get; init; } = [];
}

file sealed class ImgflipCaptionResponse
{
    [JsonPropertyName("success")]       public bool   Success      { get; init; }
    [JsonPropertyName("error_message")] public string ErrorMessage { get; init; } = string.Empty;
    [JsonPropertyName("data")]          public ImgflipCaptionData Data { get; init; } = new();
}

file sealed class ImgflipCaptionData
{
    [JsonPropertyName("url")]      public string Url     { get; init; } = string.Empty;
    [JsonPropertyName("page_url")] public string PageUrl { get; init; } = string.Empty;
}
