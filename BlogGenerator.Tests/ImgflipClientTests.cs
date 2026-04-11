using BlogGenerator.Core.Memes;

namespace BlogGenerator.Tests;

public class ImgflipClientTests
{
    private static IReadOnlyDictionary<string, ImgflipTemplate> MakeTemplates(
        params (string Name, string Id)[] entries) =>
        entries.ToDictionary(
            e => e.Name.ToLowerInvariant(),
            e => new ImgflipTemplate { Id = e.Id, Name = e.Name, Url = $"https://i.imgflip.com/{e.Id}.jpg" });

    [Fact]
    public void FindTemplate_ExactMatch()
    {
        var templates = MakeTemplates(("Drake Hotline Bling", "181913649"));
        var result = ImgflipClient.FindTemplate("Drake Hotline Bling", templates);
        Assert.NotNull(result);
        Assert.Equal("181913649", result.Id);
    }

    [Fact]
    public void FindTemplate_CaseInsensitiveExact()
    {
        var templates = MakeTemplates(("Drake Hotline Bling", "181913649"));
        var result = ImgflipClient.FindTemplate("drake hotline bling", templates);
        Assert.NotNull(result);
        Assert.Equal("181913649", result.Id);
    }

    [Fact]
    public void FindTemplate_PrefixMatch()
    {
        var templates = MakeTemplates(("Drake Hotline Bling", "181913649"));
        var result = ImgflipClient.FindTemplate("drake", templates);
        Assert.NotNull(result);
        Assert.Equal("181913649", result.Id);
    }

    [Fact]
    public void FindTemplate_SubstringMatch()
    {
        var templates = MakeTemplates(("Drake Hotline Bling", "181913649"));
        var result = ImgflipClient.FindTemplate("hotline", templates);
        Assert.NotNull(result);
        Assert.Equal("181913649", result.Id);
    }

    [Fact]
    public void FindTemplate_ReturnsNullWhenNoMatch()
    {
        var templates = MakeTemplates(("Drake Hotline Bling", "181913649"));
        var result = ImgflipClient.FindTemplate("distracted boyfriend", templates);
        Assert.Null(result);
    }
}
