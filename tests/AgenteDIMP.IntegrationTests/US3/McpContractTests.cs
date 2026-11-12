using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Infrastructure.Rag;
using Api.RAG.Application.Contracts;
using Api.RAG.Domain.Knowledge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US3;

[Collection("FoundationDatabase")]
public class McpContractTests(FoundationDatabaseFixture fixture)
{
    private WebApplicationFactory<Api.RAG.Program> CreateRagHost() =>
        new WebApplicationFactory<Api.RAG.Program>().WithWebHostBuilder(b => b.UseEnvironment("LocalDeterministic")
            .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:KnowledgeDb"] = fixture.RagConnection,
                ["DIMP_TEST_MODE"] = "compose-local"
            })));

    private async Task<string> SeedRevisionAsync()
    {
        await using var db = fixture.CreateRag();
        var sha = new string('a', 64);

        var existing = await db.DocumentRevisions.FindAsync([Guid.Empty]);
        var rev = db.DocumentRevisions.FirstOrDefault(r => r.RevisionSha256 == sha);
        if (rev == null)
        {
            var doc = db.Documents.FirstOrDefault() ?? new Document
            {
                Id = Guid.NewGuid(),
                Identification = "Manual DIMP Teste"
            };
            if (!db.Documents.Contains(doc)) db.Documents.Add(doc);

            rev = new DocumentRevision
            {
                Id = Guid.NewGuid(),
                DocumentId = doc.Id,
                RevisionSha256 = sha,
                LayoutVersion = "10",
                PageCount = 30,
                ByteSize = 100000,
                SourceStatus = "oficial_publicado",
                SourceUrl = new Uri("https://www.confaz.fazenda.gov.br/manual.pdf")
            };
            db.DocumentRevisions.Add(rev);

            var chunk = new KnowledgeChunk
            {
                Id = Guid.NewGuid(),
                RevisionId = rev.Id,
                Text = "REGISTRO 0000: ABERTURA DO ARQUIVO DIGITAL E IDENTIFICACAO DA ENTIDADE",
                Section = "REGISTRO 0000",
                RecordCode = "0000",
                Pages = [1, 2],
                ChunkHash = "chunk-test-0000-hash",
                OrderIndex = 1
            };
            db.KnowledgeChunks.Add(chunk);

            var embProfile = db.EmbeddingProfiles.Find(["local-fake-sha256-v1"]) ?? new EmbeddingProfile
            {
                ProfileId = "local-fake-sha256-v1",
                Provider = "fake",
                Model = "fake-offline-embedding",
                Dimension = 1536
            };
            if (!db.EmbeddingProfiles.Contains(embProfile)) db.EmbeddingProfiles.Add(embProfile);

            var vecValues = new float[1536];
            vecValues[0] = 1.0f;
            db.ChunkVectors.Add(new ChunkVector
            {
                ChunkId = chunk.Id,
                EmbeddingProfileId = embProfile.ProfileId,
                Vector = new Pgvector.Vector(vecValues)
            });

            var publication = new IngestionJob
            {
                DocumentId = rev.DocumentId,
                RevisionSha256 = sha,
                Status = "concluida",
                Stage = "publicacao",
                ExtractionProfileId = "pdfpig-v1",
                ChunkProfileId = "chunk-layout10-section-record-v1",
                EmbeddingProfileId = embProfile.ProfileId
            };
            db.IngestionJobs.Add(publication);
            db.IngestionChunkCheckpoints.Add(new IngestionChunkCheckpoint
            {
                IngestionJobId = publication.IngestionId,
                ChunkHash = chunk.ChunkHash,
                EmbeddingProfileId = embProfile.ProfileId,
                State = "persisted"
            });

            await db.SaveChangesAsync();
        }

        return sha;
    }

    [Fact]
    public async Task Initialize_ReturnsProtocolVersionAndServerInfo()
    {
        await using var host = CreateRagHost();
        using var client = host.CreateClient();

        var initPayload = new
        {
            jsonrpc = "2.0",
            id = "1",
            method = "initialize"
        };

        var response = await client.PostAsync("/mcp", new StringContent(JsonSerializer.Serialize(initPayload), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body) as JsonObject;

        Assert.Equal("2026-07-28", node?["result"]?["protocolVersion"]?.GetValue<string>());
        Assert.Equal("Api.RAG", node?["result"]?["serverInfo"]?["name"]?.GetValue<string>());
    }

    [Fact]
    public async Task ToolsList_ReturnsDeclaredTools()
    {
        await using var host = CreateRagHost();
        using var client = host.CreateClient();

        var listPayload = new
        {
            jsonrpc = "2.0",
            id = "2",
            method = "tools/list"
        };

        var response = await client.PostAsync("/mcp", new StringContent(JsonSerializer.Serialize(listPayload), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body) as JsonObject;
        var tools = node?["result"]?["tools"]?.AsArray();

        Assert.NotNull(tools);
        Assert.True(tools.Count >= 4);

        var names = tools.Select(t => t?["name"]?.GetValue<string>()).ToList();
        Assert.Contains("search_manual", names);
        Assert.Contains("get_manual_chunk", names);
        Assert.Contains("get_document_revision", names);
        Assert.Contains("get_ingestion_usage", names);
    }

    [Fact]
    public async Task SearchManual_WithValidRevision_ReturnsExpectedHits()
    {
        var sha = await SeedRevisionAsync();
        await using var host = CreateRagHost();
        using var client = host.CreateClient();
        var mcpClient = new RagMcpClient(client);

        var result = await mcpClient.SearchManualAsync(
            query: "Qual a regra do Registro 0000?",
            layoutVersion: "10",
            revisionSha256: sha,
            embeddingProfileId: "local-fake-sha256-v1",
            recordCodes: ["0000"]);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Hits);
        Assert.Equal("cosine_distance", result.Hits[0].RankingMetric.Name);
        Assert.Equal(sha, result.Hits[0].DocumentRevisionSha256);
    }

    [Fact]
    public async Task SearchManual_WithUnknownRevision_ReturnsTypedError()
    {
        await using var host = CreateRagHost();
        using var client = host.CreateClient();
        var mcpClient = new RagMcpClient(client);

        var unknownSha = new string('b', 64);
        var result = await mcpClient.SearchManualAsync(
            query: "Regra qualquer",
            layoutVersion: "10",
            revisionSha256: unknownSha,
            embeddingProfileId: "local-fake-sha256-v1");

        Assert.NotNull(result);
        Assert.Empty(result.Hits);
        Assert.Contains("REVISION_NOT_FOUND", result.LimitationCodes);
    }

    [Fact]
    public async Task GetDocumentRevision_WithExistingRevision_ReturnsMetadata()
    {
        var sha = await SeedRevisionAsync();
        await using var host = CreateRagHost();
        using var client = host.CreateClient();
        var mcpClient = new RagMcpClient(client);

        var result = await mcpClient.GetDocumentRevisionAsync(sha);
        Assert.NotNull(result);
        Assert.Equal(sha, result.DocumentRevisionSha256);
        Assert.Equal("10", result.LayoutVersion);
        Assert.True(result.PageCount > 0);
    }

    [Fact]
    public async Task GetManualChunk_WithUnknownChunk_ReturnsNull()
    {
        var sha = await SeedRevisionAsync();
        await using var host = CreateRagHost();
        using var client = host.CreateClient();
        var mcpClient = new RagMcpClient(client);

        var result = await mcpClient.GetManualChunkAsync(Guid.NewGuid(), sha);
        Assert.Null(result);
    }
}
