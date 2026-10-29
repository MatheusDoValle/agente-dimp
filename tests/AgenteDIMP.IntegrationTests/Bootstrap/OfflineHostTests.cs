using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Bootstrap;

public class OfflineHostTests
{
    [Fact]
    public async Task AgentStartsWithoutSecretsOrDatabases()
    {
        await using var factory = new WebApplicationFactory<Api.Agente.Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LocalDeterministic"));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bootstrap_only", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/analises")).StatusCode);
    }

    [Fact]
    public async Task RagStartsWithoutSecretsOrDatabases()
    {
        await using var factory = new WebApplicationFactory<Api.RAG.Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("LocalDeterministic"));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bootstrap_only", await response.Content.ReadAsStringAsync());
    }
}
