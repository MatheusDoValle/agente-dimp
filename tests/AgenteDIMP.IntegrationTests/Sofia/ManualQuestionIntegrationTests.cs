using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Agente.Application.Sofia;
using Api.Agente.Infrastructure.Rag;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Sofia;

public sealed class ManualQuestionIntegrationTests
{
    private const string Token = "demo-integration-test";
    private const string Revision = "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a";
    private const string Profile = "text-embedding-3-small-1536";

    [Fact]
    public async Task OpenQuestion_UsesPinnedManualAndMasksIdentifiersBeforeMcp()
    {
        var calls = 0;
        var chunkId = Guid.NewGuid();
        await using var host = CreateHost(async (request, ct) =>
        {
            calls++;
            Assert.Equal("/mcp", request.RequestUri!.AbsolutePath);
            using var rpc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var parameters = rpc.RootElement.GetProperty("params");
            Assert.Equal("search_manual", parameters.GetProperty("name").GetString());
            var arguments = parameters.GetProperty("arguments");
            Assert.Equal("10", arguments.GetProperty("layoutVersion").GetString());
            Assert.Equal(Revision, arguments.GetProperty("documentRevisionSha256").GetString());
            Assert.Equal(Profile, arguments.GetProperty("embeddingProfileId").GetString());
            var query = arguments.GetProperty("query").GetString();
            Assert.Contains("[CPF]", query);
            Assert.Contains("[CNPJ]", query);
            Assert.Contains("[EMAIL]", query);
            Assert.DoesNotContain("123.456.789-00", query);
            Assert.DoesNotContain("12.345.678/0001-90", query);
            Assert.DoesNotContain("pessoa@example.com", query);
            return McpResponse([Hit(chunkId, Revision)], []);
        });
        using var client = host.CreateClient();

        var result = await Ask(client, "Como relaciono um comprovante de pagamento ao resumo diário? 123.456.789-00 12.345.678/0001-90 pessoa@example.com");

        Assert.Equal(1, calls);
        Assert.Equal("concluido", result.Status);
        Assert.Empty(result.Achados);
        Assert.Contains("p. 40", result.Analise);
        Assert.Contains("#page=40", result.Analise);
        Assert.Contains(Revision, result.Analise);
        Assert.Contains(chunkId.ToString(), result.Analise);
    }

    [Theory]
    [InlineData("Qual registro tem a data da venda?", "DT_OP")]
    [InlineData("O que é DIMP?", "Declaração de Informações")]
    [InlineData("Como você pode me ajudar?", "Posso explicar")]
    [InlineData("O que eu devo fazer?", "Se você está começando")]
    [InlineData("Preciso corrigir algo?", "anexe a DIMP")]
    public async Task CatalogAndGeneralHelp_AreAvailableWithoutMcp(string question, string expected)
    {
        await using var host = CreateHost((_, _) => throw new InvalidOperationException("MCP should not be called."));
        using var client = host.CreateClient();

        var result = await Ask(client, question);

        Assert.Equal("concluido", result.Status);
        Assert.Contains(expected, result.Analise);
    }

    [Theory]
    [InlineData("none", "concluido", "NO_EVIDENCE_FOUND")]
    [InlineData("unavailable", "parcial", "MCP_UNAVAILABLE")]
    [InlineData("wrong-revision", "parcial", "MCP_UNAVAILABLE")]
    public async Task OpenQuestion_DistinguishesMissingEvidenceFromDependencyFailure(
        string scenario, string status, string limitation)
    {
        await using var host = CreateHost((_, _) => Task.FromResult(scenario switch
        {
            "none" => McpResponse([], ["NO_EVIDENCE_FOUND"]),
            "wrong-revision" => McpResponse([Hit(Guid.NewGuid(), new string('b', 64))], []),
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        }));
        using var client = host.CreateClient();

        var result = await Ask(client, "Qual informação consta no comprovante de pagamento?");

        Assert.Equal(status, result.Status);
        Assert.Contains(limitation, result.Observacoes!);
        Assert.DoesNotContain("#page=", result.Analise);
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedToDependencyFailure()
    {
        using var http = new HttpClient(new StubMcpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("Canceled request unexpectedly completed.");
        })) { BaseAddress = new Uri("http://127.0.0.1:5081") };
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var client = new RagMcpClient(http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SearchManualAsync("Qual campo tem a data?", "10", Revision, Profile, cancellationToken: cancel.Token));
    }

    [Fact]
    public async Task HttpTimeout_ReturnsDependencyFailure()
    {
        using var http = new HttpClient(new StubMcpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("Timed-out request unexpectedly completed.");
        }))
        {
            BaseAddress = new Uri("http://127.0.0.1:5081"),
            Timeout = TimeSpan.FromMilliseconds(150)
        };
        var client = new RagMcpClient(http);

        var result = await client.SearchManualAsync("Qual campo tem a data?", "10", Revision, Profile);

        Assert.Equal("MCP_UNAVAILABLE", Assert.Single(result.LimitationCodes));
    }

    [Fact]
    public async Task DisabledSearch_PreservesGeneralHelpWithoutMcp()
    {
        await using var host = CreateHost((_, _) => throw new InvalidOperationException("MCP should not be called."),
            manualSearchEnabled: false);
        using var client = host.CreateClient();

        var result = await Ask(client, "Qual informação consta no comprovante de pagamento?");

        Assert.Equal("concluido", result.Status);
        Assert.Contains("Posso responder dúvidas gerais", result.Analise);
    }

    [Fact]
    public async Task HostedDemo_RejectsMultipartBodyAboveFourMiB()
    {
        await using var host = CreateHost((_, _) => throw new InvalidOperationException("MCP should not be called."),
            environment: "Production");
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/demo/dimp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[4 * 1024 * 1024]), "arquivo", "sintetico.txt");
        request.Content = form;

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("4 MiB", await response.Content.ReadAsStringAsync());
    }

    private static WebApplicationFactory<Api.Agente.Program> CreateHost(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle,
        bool manualSearchEnabled = true,
        string environment = "Development") =>
        new DemoHost(manualSearchEnabled).WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureServices(services => services.AddHttpClient<IRagMcpClient, RagMcpClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubMcpHandler(handle))));

    private sealed class DemoHost(bool manualSearchEnabled) : WebApplicationFactory<Api.Agente.Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ClassroomDemo:Enabled"] = "true",
                ["ClassroomDemo:Token"] = Token,
                ["ClassroomDemo:ManualSearchEnabled"] = manualSearchEnabled ? "true" : "false"
            }));
            return base.CreateHost(builder);
        }
    }

    private static async Task<ResultadoAnaliseSofia> Ask(HttpClient client, string question)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/demo/dimp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(question), "pergunta");
        request.Content = form;
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ResultadoAnaliseSofia>())!;
    }

    private static McpHit Hit(Guid id, string revision) => new(id, Guid.NewGuid(), revision, "10", Profile,
        [40], "Registro 1115", ["1115"], [], "A soma dos valores do registro 1115 deve ser igual ao valor informado no campo 04 do Registro 1110.",
        new("cosine_distance", 0.3), "lexical");

    private static HttpResponseMessage McpResponse(IReadOnlyList<McpHit> hits, IReadOnlyList<string> limitations)
    {
        var result = JsonSerializer.Serialize(new McpSearchResult(Guid.NewGuid(), hits, limitations, []),
            JsonSerializerOptions.Web);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { result = new { content = new[] { new { text = result } } } })
        };
    }

    private sealed class StubMcpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            handle(request, ct);
    }
}
