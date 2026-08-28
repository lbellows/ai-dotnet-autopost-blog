using BlogGenerator.Core.Providers;

namespace BlogGenerator.Tests;

public class ProviderSupportTests
{
    [Fact]
    public void ModelCandidatesPutsPrimaryFirstAndDedupes()
    {
        var candidates = ProviderSupport.ModelCandidates(
            " grok-4-6 ", ["claude-sonnet-5", "GROK-4-6", "  ", "zai-org-glm-5-2"]);

        Assert.Equal(["grok-4-6", "claude-sonnet-5", "zai-org-glm-5-2"], candidates);
    }

    [Fact]
    public void ModelCandidatesFallsBackWhenPrimaryEmpty()
    {
        Assert.Equal(["claude-sonnet-5"], ProviderSupport.ModelCandidates("", ["claude-sonnet-5"]));
    }

    [Fact]
    public void ModelCandidatesKeepsFoundryDefaultAhead()
    {
        var candidates = ProviderSupport.ModelCandidates("gpt-5.4-mini", ["gpt-5.4-mini", "gpt-5-mini"]);

        Assert.Equal(["gpt-5.4-mini", "gpt-5-mini"], candidates);
    }

    [Fact]
    public void RedactReplacesSecretsWithPlaceholders()
    {
        var message = ProviderSupport.Redact(
            "call to https://example.openai.azure.com/ with key sk-abc123 failed",
            ("https://example.openai.azure.com/", "FOUNDRY_OPENAI_ENDPOINT"),
            ("sk-abc123", "FOUNDRY_PROJECT_API_KEY"));

        Assert.DoesNotContain("sk-abc123", message);
        Assert.DoesNotContain("example.openai.azure.com", message);
        Assert.Contains("[FOUNDRY_PROJECT_API_KEY]", message);
    }

    [Fact]
    public void RequireEnvThrowsWhenNoNameIsSet()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => ProviderSupport.RequireEnv("nothing set", "BLOGGEN_TEST_ABSENT_A", "BLOGGEN_TEST_ABSENT_B"));

        Assert.Equal("nothing set", ex.Message);
    }

    [Fact]
    public void RequireEnvTakesTheFirstNameThatIsSet()
    {
        Environment.SetEnvironmentVariable("BLOGGEN_TEST_SECOND", "  value  ");
        try
        {
            Assert.Equal(
                "value",
                ProviderSupport.RequireEnv("nothing set", "BLOGGEN_TEST_ABSENT_A", "BLOGGEN_TEST_SECOND"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BLOGGEN_TEST_SECOND", null);
        }
    }
}
