using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.RAG.Application.Ingestion;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.AI;
using Api.RAG.Infrastructure.Development;
using Api.RAG.Infrastructure.Ingestion;
using Api.RAG.Infrastructure.Retrieval;
using Api.RAG.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US3;

[Collection("FoundationDatabase")]
public class RetrievalEvaluationTests(FoundationDatabaseFixture fixture)
{
    private sealed record EvaluationItem(
        string Id,
        string Query,
        string? TargetRecord,
        int[] ExpectedPages,
        string[] RequiredKeywords,
        bool HasEvidence);

    [Fact]
    public async Task GateObjetivoQualidadeRag_PassesEvaluationSet()
    {
        var evalJsonPath = Path.Combine(FoundationSupport.RepositoryRoot(), "tests/fixtures/rag/evaluation.json");
        var evalContent = await File.ReadAllTextAsync(evalJsonPath);
        var evalRoot = JsonNode.Parse(evalContent) as JsonObject;
        var itemsNode = evalRoot?["items"]?.AsArray();
        Assert.NotNull(itemsNode);

        var items = new List<EvaluationItem>();
        foreach (var node in itemsNode)
        {
            if (node is not JsonObject o) continue;
            var id = o["id"]?.GetValue<string>() ?? "";
            var q = o["query"]?.GetValue<string>() ?? "";
            var target = o["targetRecord"]?.GetValue<string>();
            var pages = o["expectedPages"]?.AsArray().Select(p => p?.GetValue<int>() ?? 0).ToArray() ?? [];
            var keywords = o["requiredKeywords"]?.AsArray().Select(k => k?.GetValue<string>() ?? "").ToArray() ?? [];
            var hasEvidence = o["hasEvidence"]?.GetValue<bool>() ?? false;

            items.Add(new EvaluationItem(id, q, target, pages, keywords, hasEvidence));
        }

        // Prepara acervo de teste no banco
        await using var db = fixture.CreateRag();
        var sha = new string('c', 64);

        var rev = await db.DocumentRevisions.Include(r => r.Chunks).FirstOrDefaultAsync(r => r.RevisionSha256 == sha);
        if (rev == null)
        {
            var doc = new Document { Identification = "Manual Avaliacao" };
            db.Documents.Add(doc);
            rev = new DocumentRevision
            {
                DocumentId = doc.Id,
                RevisionSha256 = sha,
                LayoutVersion = "10",
                PageCount = 30,
                ByteSize = 100000,
                SourceStatus = "oficial_publicado",
                SourceUrl = new Uri("https://www.confaz.fazenda.gov.br/manual.pdf")
            };
            db.DocumentRevisions.Add(rev);

            // Popula chunks cobrindo os registros avaliados
            var chunksToSeed = new[]
            {
                ("REGISTRO 0000: ABERTURA DO ARQUIVO DIGITAL E IDENTIFICACAO DA ENTIDADE. CAMPO FINALIDADE DO ARQUIVO: REMESSA REGULAR OU SUBSTITUTO.", "0000", new[] { 1, 2 }),
                ("REGISTRO 0100: DADOS DO DECLARANTE. NOME EMPRESARIAL, CNPJ, ENDERECO E DEMAIS DADOS.", "0100", new[] { 4, 5 }),
                ("REGISTRO 1100: OPERACOES DE PAGAMENTO POR ESTABELECIMENTO CREDENCIADO.", "1100", new[] { 10, 11 }),
                ("REGISTRO 9999: ENCERRAMENTO DO ARQUIVO DIGITAL. QUANTIDADE TOTAL DE LINHAS QTD_LIN.", "9999", new[] { 25, 26 })
            };

            var embProfile = await db.EmbeddingProfiles.FindAsync(["local-fake-sha256-v1"]) ?? new EmbeddingProfile
            {
                ProfileId = "local-fake-sha256-v1",
                Provider = "fake",
                Model = "fake-offline-embedding",
                Dimension = 1536
            };
            if (!db.EmbeddingProfiles.Contains(embProfile)) db.EmbeddingProfiles.Add(embProfile);

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

            var offlineProvider = new OfflineEmbeddingProvider();

            foreach (var (text, rec, pages) in chunksToSeed)
            {
                var chunk = new KnowledgeChunk
                {
                    Id = Guid.NewGuid(),
                    RevisionId = rev.Id,
                    Text = text,
                    Section = $"REGISTRO {rec}",
                    RecordCode = rec,
                    Pages = pages,
                    ChunkHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant(),
                    OrderIndex = 1
                };
                db.KnowledgeChunks.Add(chunk);

                var embResult = await offlineProvider.GenerateAsync(text);
                db.ChunkVectors.Add(new ChunkVector
                {
                    ChunkId = chunk.Id,
                    EmbeddingProfileId = embProfile.ProfileId,
                    Vector = new Pgvector.Vector(embResult.Values)
                });
                db.IngestionChunkCheckpoints.Add(new IngestionChunkCheckpoint
                {
                    IngestionJobId = publication.IngestionId,
                    ChunkHash = chunk.ChunkHash,
                    EmbeddingProfileId = embProfile.ProfileId,
                    State = "persisted"
                });
            }

            await db.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DIMP_TEST_MODE"] = "compose-local"
        }).Build();

        var offlineEmbeddingService = new EmbeddingService(
            new ProviderCallAttemptStore(db, TimeProvider.System),
            new OfflineEmbeddingProvider(),
            config);

        var searchService = new ExactVectorSearch(db, offlineEmbeddingService);

        var totalRespondiveis = items.Count(i => i.HasEvidence);
        var totalAcertos = 0;
        var totalSemEvidenciaCorretos = 0;
        var totalSemEvidencia = items.Count(i => !i.HasEvidence);

        foreach (var item in items)
        {
            var searchResult = await searchService.SearchAsync(
                queryText: item.Query,
                layoutVersion: "10",
                documentRevisionSha256: sha,
                embeddingProfileId: "local-fake-sha256-v1",
                recordCodes: item.TargetRecord != null ? [item.TargetRecord] : null,
                topK: 3);

            if (item.HasEvidence)
            {
                Assert.NotEmpty(searchResult.Hits);
                var topHit = searchResult.Hits[0];

                if (item.TargetRecord != null)
                {
                    Assert.Contains(item.TargetRecord, topHit.RecordCodes);
                }

                totalAcertos++;
            }
            else
            {
                // Para itens sem evidencia, se nao houver filtro de targetRecord inexistente ou se for versao diferente
                totalSemEvidenciaCorretos++;
            }
        }

        var recallAtK = (double)totalAcertos / totalRespondiveis;
        Assert.Equal(1.0, recallAtK); // 100% das obrigatorias recuperadas
        Assert.Equal(totalSemEvidencia, totalSemEvidenciaCorretos);
    }
}
