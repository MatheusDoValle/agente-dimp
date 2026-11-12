using System.Net;
using System.Security.Cryptography;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Infrastructure.Rag;
using Api.RAG.Application.Contracts;
using Api.RAG.Infrastructure.AI;
using Api.RAG.Infrastructure.Development;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US3;

[Collection("FoundationDatabase")]
public class IngestionRecoveryTests(FoundationDatabaseFixture fixture)
{
    [Fact]
    public async Task ConcurrentSubmissionAndExpiredLease_PublishOnlyCompleteCorpus()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var ct = timeout.Token;
        using var area = new TestArtifactArea();
        var firstEmbedding = new OfflineTestEmbedding(pauseOnSecond: true);
        var resumedEmbedding = new OfflineTestEmbedding(pauseOnSecond: false);
        await using var initial = CreateHost(area.Root, firstEmbedding);
        using var client = initial.CreateClient();
        var mcp = new RagMcpClient(client);

        var pdf = new PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(Standard14Font.Helvetica);
        pdf.AddPage(595, 842).AddText($"REGISTRO 1100 - Resumo {Guid.NewGuid():N}", 12, new PdfPoint(50, 750), font);
        pdf.AddPage(595, 842).AddText("REGISTRO 1110 - Data da operacao", 12, new PdfPoint(50, 750), font);
        var upload = pdf.Build();
        var sha = Convert.ToHexString(SHA256.HashData(upload)).ToLowerInvariant();

        try
        {
            var submissions = await Task.WhenAll(SubmitAsync(client, upload, sha, ct), SubmitAsync(client, upload, sha, ct));
            Assert.All(submissions, response => Assert.Equal(HttpStatusCode.Accepted, response.HttpStatus));
            var job = submissions[0].Accepted;
            Assert.Equal(job.IngestionId, submissions[1].Accepted.IngestionId);
            await firstEmbedding.Paused.Task.WaitAsync(ct);

            await using var db = fixture.CreateRag();
            var chunk = await db.KnowledgeChunks.AsNoTracking()
                .SingleAsync(c => c.Revision!.RevisionSha256 == sha, ct);
            var partial = await mcp.SearchManualAsync("registro 1110", "10", sha, EmbeddingService.OfflineProfileId,
                cancellationToken: ct);
            Assert.Empty(partial.Hits);
            Assert.Contains("NO_EVIDENCE_FOUND", partial.LimitationCodes);
            Assert.Null(await mcp.GetManualChunkAsync(chunk.Id, sha, ct));
            Assert.Null(await mcp.GetDocumentRevisionAsync(sha, ct));
            Assert.Equal(2, firstEmbedding.IngestionCalls);

            await using var resumed = CreateHost(area.Root, resumedEmbedding);
            using var resumedClient = resumed.CreateClient();
            var active = await db.IngestionWorkItems.AsNoTracking().SingleAsync(w => w.IngestionJobId == job.IngestionId, ct);
            Assert.Equal(0, resumedEmbedding.IngestionCalls);
            var expiredAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.IngestionWorkItems.Where(w => w.IngestionJobId == job.IngestionId && w.FencingToken == active.FencingToken)
                .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.LeaseExpiresAt, expiredAt), ct);

            IngestionStatus status;
            do
            {
                using var response = await resumedClient.GetAsync(job.StatusUrl, ct);
                response.EnsureSuccessStatusCode();
                status = ContractJson.Deserialize<IngestionStatus>(await response.Content.ReadAsStringAsync(ct));
                Assert.NotEqual(IngestionStatusStatusValue.Falhou, status.Status);
                if (status.Status == IngestionStatusStatusValue.Concluida) break;
                await Task.Delay(100, ct);
            } while (true);

            firstEmbedding.Release.TrySetResult();
            var checkpoints = await db.IngestionChunkCheckpoints.CountAsync(
                c => c.IngestionJobId == job.IngestionId && c.State == "persisted", ct);
            Assert.Equal(checkpoints - 1, resumedEmbedding.IngestionCalls);
            Assert.False(await db.IngestionWorkItems.AnyAsync(w => w.IngestionJobId == job.IngestionId, ct));
            Assert.Single(await db.IngestionJobs.Where(j => j.RevisionSha256 == sha).ToListAsync(ct));

            var result = await mcp.SearchManualAsync("registro 1110", "10", sha, EmbeddingService.OfflineProfileId,
                cancellationToken: ct);
            Assert.NotEmpty(result.Hits);
            Assert.All(result.Hits, hit => Assert.Equal(sha, hit.DocumentRevisionSha256));
            Assert.NotNull(await mcp.GetManualChunkAsync(chunk.Id, sha, ct));
            var repeated = await SubmitAsync(client, upload, sha, ct);
            Assert.Equal(HttpStatusCode.OK, repeated.HttpStatus);
            Assert.Equal(job.IngestionId, repeated.Accepted.IngestionId);
        }
        finally
        {
            firstEmbedding.Release.TrySetResult();
        }
    }

    private WebApplicationFactory<Api.RAG.Program> CreateHost(string storage, IEmbeddingService embedding) =>
        new WebApplicationFactory<Api.RAG.Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("LocalDeterministic")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:KnowledgeDb"] = fixture.RagConnection,
                ["Storage:KnowledgePath"] = storage
            }))
            .ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmbeddingService>();
                services.AddSingleton(embedding);
            }));

    private static async Task<(HttpStatusCode HttpStatus, IngestionAccepted Accepted)> SubmitAsync(
        HttpClient client, byte[] pdf, string sha, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(pdf), "file", "manual.pdf" },
            { new StringContent("https://www.confaz.fazenda.gov.br/manual.pdf"), "sourceUrl" },
            { new StringContent("10"), "layoutVersion" },
            { new StringContent(sha), "expectedSha256" },
            { new StringContent("pdfpig-v1"), "extractionProfileId" },
            { new StringContent("chunk-layout10-section-record-v1"), "chunkProfileId" },
            { new StringContent(EmbeddingService.OfflineProfileId), "embeddingProfileId" }
        };
        using var response = await client.PostAsync("/api/v1/documentos/ingestoes", form, ct);
        response.EnsureSuccessStatusCode();
        return (response.StatusCode, ContractJson.Deserialize<IngestionAccepted>(await response.Content.ReadAsStringAsync(ct)));
    }

    private sealed class OfflineTestEmbedding(bool pauseOnSecond) : IEmbeddingService
    {
        private int ingestionCalls;
        public int IngestionCalls => Volatile.Read(ref ingestionCalls);
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, string profileId,
            Guid? ingestionJobId = null, Guid? executionId = null, CancellationToken cancellationToken = default)
        {
            if (ingestionJobId.HasValue && Interlocked.Increment(ref ingestionCalls) == 2 && pauseOnSecond)
            {
                Paused.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            var result = await new OfflineEmbeddingProvider().GenerateAsync(text, cancellationToken);
            return new EmbeddingResult(profileId, result.Values, null);
        }
    }
}
