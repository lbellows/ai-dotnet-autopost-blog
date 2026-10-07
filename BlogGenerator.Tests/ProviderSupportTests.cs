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
    public void ModelCandidatesKeepsPrimaryAhead()
    {
        var candidates = ProviderSupport.ModelCandidates("gpt-5.4-mini", ["gpt-5.4-mini", "gpt-5-mini"]);

        Assert.Equal(["gpt-5.4-mini", "gpt-5-mini"], candidates);
    }

    [Fact]
    public void RedactReplacesSecretsWithPlaceholders()
    {
        var message = ProviderSupport.Redact(
            "call to http://main:8080/ with key sk-abc123 failed",
            ("http://main:8080/", "LOCAL_AI_BASE_URL"),
            ("sk-abc123", "VENICE_API_KEY"));

        Assert.DoesNotContain("sk-abc123", message);
        Assert.DoesNotContain("main:8080", message);
        Assert.Contains("[VENICE_API_KEY]", message);
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
