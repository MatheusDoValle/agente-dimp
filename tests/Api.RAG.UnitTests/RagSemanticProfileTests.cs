using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Development;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.RAG.UnitTests;

public sealed class RagSemanticProfileTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void LocalRealRequiresExplicitActivationAndKey(bool externalCalls, bool hasKey, bool accepted)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentProfile:Name"] = "LocalReal",
            ["DevelopmentProfile:Provider"] = "OpenAI",
            ["DevelopmentProfile:ExternalCallsEnabled"] = externalCalls.ToString(),
            ["Processing:Model"] = "text-embedding-3-small",
            ["OpenAI:ApiKey"] = hasKey ? "test-placeholder" : null
        }).Build();

        if (accepted) BootstrapProfile.Validate(configuration);
        else Assert.Throws<InvalidOperationException>(() => BootstrapProfile.Validate(configuration));
    }

    [Fact]
    public void SemanticProfileRejectsAnInvalidBudgetAndDistance()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentProfile:ExternalCallsEnabled"] = "true"
        }).Build();
        var validator = new RagOptionsValidator(configuration);
        var options = new RagOptions
        {
            Model = "text-embedding-3-small",
            GlobalDeadlineSeconds = 900,
            ProviderTimeoutSeconds = 30,
            MaximumProviderCalls = 0,
            TokenBudget = 1000000,
            MaximumCosineDistance = 2.1
        };

        var validation = validator.Validate(null, options);
        Assert.True(validation.Failed);
        Assert.Equal(2, validation.Failures!.Count());
    }
}
