using Api.Agente.Infrastructure.Development;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Agente.UnitTests;

public class BootstrapProfileTests
{
    [Theory]
    [InlineData("LocalReal", false, "Fake")]
    [InlineData("LocalDeterministic", true, "Fake")]
    [InlineData("LocalDeterministic", false, "OpenAI")]
    public void RejectsProfilesThatCouldEnableExternalProcessing(string name, bool enabled, string provider)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentProfile:Name"] = name,
            ["DevelopmentProfile:Provider"] = provider,
            ["DevelopmentProfile:ExternalCallsEnabled"] = enabled.ToString()
        }).Build();
        Assert.Throws<InvalidOperationException>(() => BootstrapProfile.Validate(config));
    }

    [Fact]
    public async Task OfflineDoubleLabelsSyntheticOutputAndDoesNotEchoInput()
    {
        var result = await new OfflineAnalysisProvider().AnalyzeAsync("sentinela-privada");
        Assert.True(result.IsSynthetic);
        Assert.DoesNotContain("sentinela-privada", result.Summary);
    }
}
