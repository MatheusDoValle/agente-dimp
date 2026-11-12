using System.Security.Cryptography;
using Api.RAG.Application.Contracts;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.AI;
using Api.RAG.Infrastructure.Ingestion;
using Api.RAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Api.RAG.Application.Ingestion;

public sealed record IngestionSubmissionResult(
    bool IsAlreadyCompleted,
    IngestionAccepted Accepted);

public sealed class DocumentIngestionService
{
    private readonly KnowledgeDbContext database;
    private readonly PdfDocumentExtractor extractor;
    private readonly DocumentChunker chunker;
    private readonly IEmbeddingService embeddingService;
    private readonly TimeProvider clock;
    private readonly IConfiguration configuration;
    private readonly IngestionQueue queue;

    public DocumentIngestionService(KnowledgeDbContext database, PdfDocumentExtractor extractor,
        DocumentChunker chunker, IEmbeddingService embeddingService, TimeProvider clock, IConfiguration configuration)
    {
        this.database = database;
        this.extractor = extractor;
        this.chunker = chunker;
        this.embeddingService = embeddingService;
        this.clock = clock;
        this.configuration = configuration;
        queue = new IngestionQueue(database, clock);
    }


    public static readonly string[] SupportedExtractionProfiles = ["pdfpig-v1", "default-extraction-v1"];
    public static readonly string[] SupportedChunkProfiles = ["chunk-layout10-section-record-v1", "default-chunk-v1"];
    public static readonly string[] SupportedEmbeddingProfiles = ["text-embedding-3-small-1536", "local-fake-sha256-v1"];

    public async Task<IngestionSubmissionResult> SubmitAsync(
        Stream pdfStream,
        string sourceUrl,
        string layoutVersion,
        string expectedSha256,
        string extractionProfileId,
        string chunkProfileId,
        string embeddingProfileId,
        string? storageDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(layoutVersion) || layoutVersion != "10")
            throw new BadHttpRequestException("LayoutVersion suportado e '10'.");

        if (!SupportedExtractionProfiles.Contains(extractionProfileId))
            throw new IngestionValidationException("PROFILE_UNKNOWN", $"Perfil de extracao '{extractionProfileId}' nao suportado.");

        if (!SupportedChunkProfiles.Contains(chunkProfileId))
            throw new IngestionValidationException("PROFILE_UNKNOWN", $"Perfil de chunking '{chunkProfileId}' nao suportado.");

        if (!SupportedEmbeddingProfiles.Contains(embeddingProfileId))
            throw new IngestionValidationException("PROFILE_UNKNOWN", $"Perfil de embedding '{embeddingProfileId}' nao suportado.");

        using var memoryStream = new MemoryStream();
        await pdfStream.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
            throw new IngestionValidationException("EMPTY_FILE", "Arquivo vazio nao e aceito.");

        // Valida se o cabeçalho é de PDF (%PDF)
        if (bytes.Length < 4 || bytes[0] != 0x25 || bytes[1] != 0x50 || bytes[2] != 0x44 || bytes[3] != 0x46)
            throw new UnsupportedMediaTypeException("Arquivo submetido nao e um documento PDF valido.");

        var actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var normalizedExpected = expectedSha256.Trim().ToLowerInvariant();

        if (!string.Equals(actualSha256, normalizedExpected, StringComparison.OrdinalIgnoreCase))
            throw new IngestionValidationException("HASH_MISMATCH", $"Hash SHA-256 calculado '{actualSha256}' diverge do esperado '{normalizedExpected}'.");


        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize registration of the document, its revision and work item.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(40602001)", cancellationToken);

        var job = await database.IngestionJobs.FirstOrDefaultAsync(j =>
            j.RevisionSha256 == actualSha256 &&
            j.ExtractionProfileId == extractionProfileId &&
            j.ChunkProfileId == chunkProfileId &&
            j.EmbeddingProfileId == embeddingProfileId, cancellationToken);
        var revision = await database.DocumentRevisions.FirstOrDefaultAsync(
            r => r.RevisionSha256 == actualSha256, cancellationToken);
        var now = clock.GetUtcNow();

        if (revision is null)
        {
            var document = await database.Documents.FirstOrDefaultAsync(cancellationToken);
            if (document is null)
            {
                document = new Document { Identification = "Manual de Orientação do Leiaute da DIMP", CreatedAt = now };
                database.Documents.Add(document);
            }

            revision = new DocumentRevision
            {
                DocumentId = document.Id,
                SourceUrl = new Uri(sourceUrl),
                RevisionSha256 = actualSha256,
                LayoutVersion = layoutVersion,
                RetrievedAt = now,
                ByteSize = bytes.Length,
                PageCount = 1
            };
            database.DocumentRevisions.Add(revision);
        }

        if (job is null || job.Status == "recebida" || job.Status == "em_processamento")
        {
            if (!File.Exists(revision.StoragePath))
                revision.StoragePath = await StorePdfAsync(bytes, actualSha256, storageDirectory, cancellationToken);
        }

        if (job is null)
        {
            job = new IngestionJob
            {
                DocumentId = revision.DocumentId,
                RevisionSha256 = actualSha256,
                ExtractionProfileId = extractionProfileId,
                ChunkProfileId = chunkProfileId,
                EmbeddingProfileId = embeddingProfileId,
                CreatedAt = now,
                UpdatedAt = now
            };
            database.IngestionJobs.Add(job);
            database.IngestionWorkItems.Add(new IngestionWorkItem
            {
                IngestionJobId = job.IngestionId,
                AvailableAt = now,
                FencingToken = 1,
                CheckpointVersion = 1
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var completed = job.Status == "concluida";
        return new IngestionSubmissionResult(completed, new IngestionAccepted
        {
            IngestionId = job.IngestionId,
            DocumentId = job.DocumentId,
            RevisionSha256 = job.RevisionSha256,
            StatusUrl = $"/api/v1/documentos/ingestoes/{job.IngestionId}",
            Status = completed ? IngestionAcceptedStatusValue.Concluida : IngestionAcceptedStatusValue.Recebida
        });
    }

    private static async Task<string> StorePdfAsync(
        byte[] bytes, string hash, string? directory, CancellationToken cancellationToken)
    {
        directory = Path.GetFullPath(directory ?? Path.Combine(Path.GetTempPath(), "AgenteDIMP.Knowledge"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{hash}.pdf");
        var temporary = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return path;
    }


    internal async Task ProcessJobAsync(IngestionLease lease, CancellationToken cancellationToken)
    {
        var job = await database.IngestionJobs.SingleAsync(j => j.IngestionId == lease.JobId, cancellationToken);
        var revision = await database.DocumentRevisions.SingleAsync(
            r => r.RevisionSha256 == job.RevisionSha256, cancellationToken);
        if (job.Status == "concluida" || job.Status == "falhou")
        {
            await queue.SaveAsync(lease, cancellationToken, complete: true);
            return;
        }

        var offline = job.EmbeddingProfileId == EmbeddingService.OfflineProfileId;
        if (!offline && (!configuration.GetValue<bool>("DevelopmentProfile:ExternalCallsEnabled") ||
            string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"])))
        {
            throw new IngestionValidationException("EMBEDDING_PROFILE_UNAVAILABLE", "Perfil de embedding indisponível.");
        }

        using var deadline = offline ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (deadline is not null)
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue<double>("Processing:GlobalDeadlineSeconds")));
            cancellationToken = deadline.Token;
        }

        job.Status = "em_processamento";
        await SetStageAsync(job, "extracao", lease, cancellationToken);

        if (!File.Exists(revision.StoragePath))
            throw new IngestionValidationException("PDF_UNAVAILABLE", "O PDF da revisão não está disponível.");
        var bytes = await File.ReadAllBytesAsync(revision.StoragePath!, cancellationToken);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(job.RevisionSha256, StringComparison.OrdinalIgnoreCase))
            throw new IngestionValidationException("HASH_MISMATCH", "O PDF armazenado não corresponde à revisão.");

        using var stream = new MemoryStream(bytes);
        ExtractedPdfDocument extractedDoc;
        try
        {
            extractedDoc = extractor.Extract(stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new IngestionValidationException("PDF_EXTRACTION_FAILED", "Falha ao extrair o PDF armazenado.");
        }

        revision.PageCount = extractedDoc.PageCount;
        revision.ByteSize = extractedDoc.ByteSize;
        await SetStageAsync(job, "segmentacao", lease, cancellationToken);

        var chunkItems = chunker.ChunkDocument(extractedDoc, new ChunkProfile(job.ChunkProfileId));
        if (chunkItems.Count == 0)
            throw new IngestionValidationException("NO_TEXT_FOUND", "O PDF não gerou trechos indexáveis.");
        // One call per chunk and one token per UTF-8 byte are conservative upper bounds.
        if (!offline && (chunkItems.Count > configuration.GetValue<int>("Processing:MaximumProviderCalls") ||
            chunkItems.Sum(item => (long)System.Text.Encoding.UTF8.GetByteCount(item.Text)) >
                configuration.GetValue<int>("Processing:TokenBudget")))
            throw new IngestionValidationException("EMBEDDING_BUDGET_EXCEEDED", "O manual excede o orçamento de embeddings.");

        await SetStageAsync(job, "embeddings", lease, cancellationToken);

        if (await database.EmbeddingProfiles.FindAsync([job.EmbeddingProfileId], cancellationToken) is null)
        {
            database.EmbeddingProfiles.Add(new EmbeddingProfile
            {
                ProfileId = job.EmbeddingProfileId,
                Provider = offline ? "fake" : "openai",
                Model = offline ? "fake-offline-embedding" : "text-embedding-3-small",
                CreatedAt = clock.GetUtcNow()
            });
            await queue.SaveAsync(lease, cancellationToken);
        }

        foreach (var item in chunkItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var checkpoint = await database.IngestionChunkCheckpoints.FindAsync(
                [job.IngestionId, item.ChunkHash, job.EmbeddingProfileId], cancellationToken);
            if (checkpoint?.State == "persisted") continue;

            if (!offline && checkpoint?.State is "calling" or "unknown")
                throw new IngestionValidationException("EMBEDDING_RECONCILIATION_REQUIRED", "Há uma tentativa de embedding com resultado desconhecido.");

            if (checkpoint is null)
            {
                checkpoint = new IngestionChunkCheckpoint
                {
                    IngestionJobId = job.IngestionId,
                    ChunkHash = item.ChunkHash,
                    EmbeddingProfileId = job.EmbeddingProfileId
                };
                database.IngestionChunkCheckpoints.Add(checkpoint);
            }

            var chunk = await database.KnowledgeChunks.FirstOrDefaultAsync(
                c => c.RevisionId == revision.Id && c.ChunkHash == item.ChunkHash, cancellationToken);
            var existingVector = chunk is null ? null : await database.ChunkVectors.FindAsync(
                [chunk.Id, job.EmbeddingProfileId], cancellationToken);
            if (existingVector is null)
            {
                checkpoint.State = "calling";
                checkpoint.UpdatedAt = clock.GetUtcNow();
                await queue.SaveAsync(lease, cancellationToken);

                EmbeddingResult embedding;
                try
                {
                    embedding = await embeddingService.GenerateEmbeddingAsync(
                        item.Text, job.EmbeddingProfileId, ingestionJobId: job.IngestionId, cancellationToken: cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    checkpoint.State = "unknown";
                    checkpoint.UpdatedAt = clock.GetUtcNow();
                    await queue.SaveAsync(lease, cancellationToken);
                    throw new IngestionValidationException("EMBEDDING_FAILED", "Falha ao gerar embedding do trecho.");
                }

                if (chunk is null)
                {
                    chunk = new KnowledgeChunk
                    {
                        RevisionId = revision.Id,
                        Text = item.Text,
                        Pages = item.Pages,
                        Section = item.Section,
                        RecordCode = item.RecordCode,
                        FieldCode = item.FieldCode,
                        ChunkHash = item.ChunkHash,
                        OrderIndex = item.OrderIndex
                    };
                    database.KnowledgeChunks.Add(chunk);
                }

                database.ChunkVectors.Add(new ChunkVector
                {
                    ChunkId = chunk.Id,
                    EmbeddingProfileId = job.EmbeddingProfileId,
                    Vector = new Vector(embedding.Vector)
                });
                checkpoint.CallAttemptId = embedding.AttemptId;
            }

            checkpoint.State = "persisted";
            checkpoint.UpdatedAt = clock.GetUtcNow();
            await queue.SaveAsync(lease, cancellationToken);
        }

        job.Status = "concluida";
        job.Stage = "publicacao";
        job.UpdatedAt = clock.GetUtcNow();
        await queue.SaveAsync(lease, cancellationToken, complete: true);
    }

    internal async Task FailJobAsync(IngestionLease lease, string code, CancellationToken cancellationToken)
    {
        var job = await database.IngestionJobs.SingleAsync(j => j.IngestionId == lease.JobId, cancellationToken);
        job.Status = "falhou";
        job.LimitationCodes = [code];
        job.UpdatedAt = clock.GetUtcNow();
        await queue.SaveAsync(lease, cancellationToken, complete: true);
    }

    private Task SetStageAsync(IngestionJob job, string stage, IngestionLease lease, CancellationToken cancellationToken)
    {
        job.Stage = stage;
        job.UpdatedAt = clock.GetUtcNow();
        return queue.SaveAsync(lease, cancellationToken);
    }
}

public sealed class IngestionValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class UnsupportedMediaTypeException(string message) : Exception(message);
