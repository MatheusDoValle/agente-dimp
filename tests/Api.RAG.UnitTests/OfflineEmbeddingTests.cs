using Api.RAG.Infrastructure.Development;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.RAG.UnitTests;

public class OfflineEmbeddingTests
{
    [Fact]
    public async Task FakeVectorsAreReproducibleAndUseADistinctProfile()
    {
        var provider = new OfflineEmbeddingProvider();
        var first = await provider.GenerateAsync("evidencia sintetica");
        var second = await provider.GenerateAsync("evidencia sintetica");
        Assert.True(first.IsSynthetic);
        Assert.StartsWith("local-fake-", first.ProfileId);
        Assert.Equal(1536, first.Values.Length);
        Assert.Equal(first.Values, second.Values);
    }

    [Fact]
    public void BootstrapRejectsRealProvider()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentProfile:Name"] = "LocalReal",
            ["DevelopmentProfile:Provider"] = "OpenAI",
            ["DevelopmentProfile:ExternalCallsEnabled"] = "true"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => BootstrapProfile.Validate(config));
    }
}
