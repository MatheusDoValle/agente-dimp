using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Agente.UnitTests.Foundation;

public class ConfigurationSnapshotTests
{
    [Fact]
    public void SnapshotUsesWhitelistedFieldsAndNeverCopiesSecrets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Processing:GlobalDeadlineSeconds"] = "120",
            ["Processing:ProviderTimeoutSeconds"] = "30",
            ["Processing:MaximumProviderCalls"] = "0",
            ["Processing:TokenBudget"] = "0",
            ["Processing:Model"] = "fake-analysis",
            ["OpenAI:ApiKey"] = "sentinela-segredo",
            ["ConnectionStrings:AgentDb"] = "sentinela-conexao"
        }).Build();
        var snapshot = new Application.ExecutionContext(TimeProvider.System, configuration).CreateConfigurationSnapshot();
        var json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("sentinela-segredo", json);
        Assert.DoesNotContain("sentinela-conexao", json);
        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(TimeSpan.Zero, snapshot.CreatedAt.Offset);
    }
}
