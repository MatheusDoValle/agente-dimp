using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Sofia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Sofia;

public sealed class A2AIntegrationTests
{
    private static WebApplicationFactory<Api.Agente.Program> CreateHost(string tempRoot) =>
        new WebApplicationFactory<Api.Agente.Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("LocalDeterministic")
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TemporaryStorage:RootPath"] = tempRoot,
                    ["ConnectionStrings:AgentDb"] = "Host=127.0.0.1;Port=1;Database=agente;Username=agente_app;Timeout=1;Command Timeout=1;Pooling=false",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                    ["Cors:AllowedOrigins:1"] = "http://localhost:5173",
                    ["A2A:PublicUrl"] = "http://agente-dimp:8080/a2a"
                })));

    [Fact]
    public async Task GetHealth_DeveRetornarStatusAlive()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"alive\"", json);
        Assert.Contains("\"service\":\"Api.Agente\"", json);
    }

    [Fact]
    public async Task GetHealthLive_DeveRetornarStatusAlive()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"alive\"", json);
    }

    [Fact]
    public async Task GetAgentCard_DeveRetornarCardValidoComSkillAnalisarDocumentos()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/.well-known/agent-card.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var card = await response.Content.ReadFromJsonAsync<AgentCard>();
        Assert.NotNull(card);
        Assert.Equal("Agente Fiscal DIMP INFIS", card.Name);
        Assert.Equal("http://agente-dimp:8080/a2a", card.Url);
        Assert.Equal("1.0.0", card.Version);
        Assert.False(card.Capabilities.Streaming);
        Assert.False(card.Capabilities.PushNotifications);
        Assert.Contains("text/plain", card.DefaultInputModes);
        Assert.Contains("text/plain", card.DefaultOutputModes);
        Assert.Single(card.Skills);
        Assert.Equal("analisar_documentos", card.Skills[0].Id);
        Assert.Contains("infis", card.Skills[0].Tags);
        Assert.Contains("dimp", card.Skills[0].Tags);
    }

    [Fact]
    public async Task PreflightCors_OrigemPermitida_DeveRetornarCabecalhosCors()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/a2a");
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type, Authorization");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:4200", response.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task PreflightCors_OrigemNaoPermitida_NaoDeveRetornarAllowOrigin()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/a2a");
        request.Headers.Add("Origin", "http://origem-desconhecida.org");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PostA2A_FluxoCompleto_DimpComInconsistencia_DeveRetornarTaskCompletedComResultadoSofia()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var dimpComErro = """
        |0000|10|
        |9999|2|
        """;

        var idExec = Guid.NewGuid();
        var idSol = Guid.NewGuid();
        var solicitacao = new SolicitacaoAnaliseSofia(
            VersaoContrato: "1.0",
            IdExecucao: idExec,
            IdSolicitacao: idSol,
            Usuario: new UsuarioSofia("Maria Oliveira"),
            Pergunta: "Verifique inconsistências fiscais no DIMP.",
            Documentos: new[]
            {
                new DocumentoSofia("doc-01", "remessa.dimp", "text/plain", dimpComErro)
            }
        );

        var payloadJson = JsonSerializer.Serialize(solicitacao, ContractJson.Options);

        var a2aRequest = new A2ARpcRequest(
            JsonRpc: "2.0",
            Id: "rpc-test-001",
            Method: "message/send",
            Params: new A2AMessageParams(
                Message: new A2AMessage(
                    Role: "user",
                    MessageId: Guid.NewGuid().ToString(),
                    Parts: new[]
                    {
                        new A2APart(Kind: "text", Text: payloadJson)
                    }
                )
            )
        );

        var requestJson = JsonSerializer.Serialize(a2aRequest);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/a2a", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var a2aResponse = await response.Content.ReadFromJsonAsync<A2ARpcResponse>();
        Assert.NotNull(a2aResponse);
        Assert.Equal("2.0", a2aResponse.JsonRpc);
        Assert.Equal("rpc-test-001", a2aResponse.Id);
        Assert.NotNull(a2aResponse.Result);
        Assert.Equal("completed", a2aResponse.Result.Status.State);
        Assert.Single(a2aResponse.Result.Artifacts);

        var artifact = a2aResponse.Result.Artifacts[0];
        Assert.Equal("analise-sofia-v1", artifact.Name);
        Assert.Single(artifact.Parts);
        Assert.Equal("text", artifact.Parts[0].Kind);

        // Desserializar o payload interno do contrato SOFIA
        var resultadoSofia = JsonSerializer.Deserialize<ResultadoAnaliseSofia>(artifact.Parts[0].Text, ContractJson.Options);
        Assert.NotNull(resultadoSofia);
        Assert.Equal("1.0", resultadoSofia.VersaoContrato);
        Assert.Equal(idExec, resultadoSofia.IdExecucao);
        Assert.Equal(idSol, resultadoSofia.IdSolicitacao);
        using var cardResponse = await client.GetAsync("/.well-known/agent-card.json");
        cardResponse.EnsureSuccessStatusCode();
        using var card = JsonDocument.Parse(await cardResponse.Content.ReadAsStringAsync());
        Assert.Equal("agente-dimp", resultadoSofia.Agente.IdAgente);
        Assert.Equal(card.RootElement.GetProperty("name").GetString(), resultadoSofia.Agente.Nome);
        Assert.Equal("concluido", resultadoSofia.Status);
        Assert.NotEmpty(resultadoSofia.Achados);
        Assert.Contains("doc-01", resultadoSofia.Achados[0].IdDocumentos);
    }

    [Fact]
    public async Task PostA2A_VersaoJsonRpcInvalida_DeveRetornarCodigo32600()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var payload = """
        {
          "jsonrpc": "1.0",
          "id": "rpc-err-1",
          "method": "message/send",
          "params": {
            "message": {
              "role": "user",
              "messageId": "msg-1",
              "parts": [{ "kind": "text", "text": "{}" }]
            }
          }
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/a2a", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errResponse = await response.Content.ReadFromJsonAsync<A2ARpcErrorResponse>();
        Assert.NotNull(errResponse);
        Assert.Equal(-32600, errResponse.Error.Code);
        Assert.Contains("Invalid JSON-RPC version", errResponse.Error.Message);
    }

    [Fact]
    public async Task PostA2A_MetodoDesconhecido_DeveRetornarCodigo32601()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var payload = """
        {
          "jsonrpc": "2.0",
          "id": "rpc-err-2",
          "method": "tasks/unknown",
          "params": {
            "message": {
              "role": "user",
              "messageId": "msg-1",
              "parts": [{ "kind": "text", "text": "{}" }]
            }
          }
        }
        """;

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/a2a", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var errResponse = await response.Content.ReadFromJsonAsync<A2ARpcErrorResponse>();
        Assert.NotNull(errResponse);
        Assert.Equal(-32601, errResponse.Error.Code);
    }

    [Fact]
    public async Task PostA2A_VersaoContratoSofiaInvalida_DeveRetornarCodigo32602()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var payloadInvalido = """
        {
          "versaoContrato": "2.0",
          "idExecucao": "8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e",
          "idSolicitacao": "e1b3a4cb-d729-4bd4-b796-b9c94be07b46",
          "usuario": { "nome": "Maria" },
          "pergunta": "Validar",
          "documentos": [{ "idDocumento": "doc1", "nome": "d.txt", "texto": "conteudo" }]
        }
        """;

        var a2aRequest = new A2ARpcRequest(
            JsonRpc: "2.0",
            Id: "rpc-err-3",
            Method: "message/send",
            Params: new A2AMessageParams(
                Message: new A2AMessage(
                    Role: "user",
                    MessageId: "msg-1",
                    Parts: new[] { new A2APart(Kind: "text", Text: payloadInvalido) }
                )
            )
        );

        using var content = new StringContent(JsonSerializer.Serialize(a2aRequest), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/a2a", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errResponse = await response.Content.ReadFromJsonAsync<A2ARpcErrorResponse>();
        Assert.NotNull(errResponse);
        Assert.Equal(-32602, errResponse.Error.Code);
        Assert.True(errResponse.Error.Message.Contains("Versao do contrato invalida"),
            $"Expected 'Versao do contrato invalida', but got: '{errResponse.Error.Message}'");
    }
}
