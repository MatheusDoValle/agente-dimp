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

public sealed class DocumentIngestionService(
    KnowledgeDbContext database,
    PdfDocumentExtractor extractor,
    DocumentChunker chunker,
    IEmbeddingService embeddingService,
    TimeProvider clock)
{
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

        // Verifica se já existe IngestionJob para este hash + perfis
        var existingJob = await database.IngestionJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j =>
                j.RevisionSha256 == actualSha256 &&
                j.ExtractionProfileId == extractionProfileId &&
                j.ChunkProfileId == chunkProfileId &&
                j.EmbeddingProfileId == embeddingProfileId,
                cancellationToken);

        if (existingJob != null)
        {
            var isCompleted = existingJob.Status == "concluida";
            return new IngestionSubmissionResult(
                IsAlreadyCompleted: isCompleted,
                Accepted: new IngestionAccepted
                {
                    IngestionId = existingJob.IngestionId,
                    DocumentId = existingJob.DocumentId,
                    RevisionSha256 = existingJob.RevisionSha256,
                    StatusUrl = $"/api/v1/documentos/ingestoes/{existingJob.IngestionId}",
                    Status = isCompleted ? IngestionAcceptedStatusValue.Concluida : IngestionAcceptedStatusValue.Recebida
                });
        }

        // Obtém ou cria Document
        var document = await database.Documents.FirstOrDefaultAsync(cancellationToken);
        if (document == null)
        {
            document = new Document
            {
                Id = Guid.NewGuid(),
                Identification = "Manual de Orientação do Leiaute da DIMP",
                CreatedAt = clock.GetUtcNow()
            };
            database.Documents.Add(document);
            await database.SaveChangesAsync(cancellationToken);
        }

        // Salva arquivo no storage se fornecido
        string? storedPath = null;
        if (!string.IsNullOrWhiteSpace(storageDirectory))
        {
            Directory.CreateDirectory(storageDirectory);
            storedPath = Path.Combine(storageDirectory, $"{actualSha256}.pdf");
            await File.WriteAllBytesAsync(storedPath, bytes, cancellationToken);
        }

        // Obtém ou cria DocumentRevision
        var revision = await database.DocumentRevisions.FirstOrDefaultAsync(r => r.RevisionSha256 == actualSha256, cancellationToken);
        if (revision == null)
        {
            revision = new DocumentRevision
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                SourceUrl = new Uri(sourceUrl),
                RevisionSha256 = actualSha256,
                LayoutVersion = layoutVersion,
                RetrievedAt = clock.GetUtcNow(),
                ByteSize = bytes.Length,
                PageCount = 1, // atualizado após extração
                SourceStatus = "oficial_publicado",
                StoragePath = storedPath
            };
            database.DocumentRevisions.Add(revision);
            await database.SaveChangesAsync(cancellationToken);
        }

        var newJob = new IngestionJob
        {
            IngestionId = Guid.NewGuid(),
            DocumentId = document.Id,
            RevisionSha256 = actualSha256,
            Status = "recebida",
            Stage = "recebimento",
            ExtractionProfileId = extractionProfileId,
            ChunkProfileId = chunkProfileId,
            EmbeddingProfileId = embeddingProfileId,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };
        database.IngestionJobs.Add(newJob);

        var workItem = new IngestionWorkItem
        {
            IngestionJobId = newJob.IngestionId,
            AvailableAt = clock.GetUtcNow(),
            FencingToken = 1,
            CheckpointVersion = 1
        };
        database.IngestionWorkItems.Add(workItem);

        await database.SaveChangesAsync(cancellationToken);

        return new IngestionSubmissionResult(
            IsAlreadyCompleted: false,
            Accepted: new IngestionAccepted
            {
                IngestionId = newJob.IngestionId,
                DocumentId = document.Id,
                RevisionSha256 = actualSha256,
                StatusUrl = $"/api/v1/documentos/ingestoes/{newJob.IngestionId}",
                Status = IngestionAcceptedStatusValue.Recebida
            });
    }

    public async Task ProcessJobAsync(Guid ingestionJobId, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var job = await database.IngestionJobs.FirstOrDefaultAsync(j => j.IngestionId == ingestionJobId, cancellationToken)
            ?? throw new KeyNotFoundException($"IngestionJob {ingestionJobId} nao encontrado.");

        if (job.Status == "concluida") return;

        job.Status = "em_processamento";
        job.Stage = "extracao";
        job.UpdatedAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);

        using var stream = new MemoryStream(pdfBytes);
        var extractedDoc = extractor.Extract(stream);

        var revision = await database.DocumentRevisions.FirstOrDefaultAsync(r => r.RevisionSha256 == job.RevisionSha256, cancellationToken);
        if (revision != null)
        {
            revision.PageCount = extractedDoc.PageCount;
            revision.ByteSize = extractedDoc.ByteSize;
        }

        job.Stage = "segmentacao";
        job.UpdatedAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);

        var chunkProfile = new ChunkProfile(job.ChunkProfileId);
        var chunkItems = chunker.ChunkDocument(extractedDoc, chunkProfile);

        job.Stage = "embeddings";
        job.UpdatedAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);

        // Garante que o EmbeddingProfile exista no banco
        var embProfile = await database.EmbeddingProfiles.FindAsync([job.EmbeddingProfileId], cancellationToken);
        if (embProfile == null)
        {
            embProfile = new EmbeddingProfile
            {
                ProfileId = job.EmbeddingProfileId,
                Provider = job.EmbeddingProfileId == EmbeddingService.OfflineProfileId ? "fake" : "openai",
                Model = job.EmbeddingProfileId == EmbeddingService.OfflineProfileId ? "fake-offline-embedding" : "text-embedding-3-small",
                Dimension = 1536,
                Metric = "cosine",
                Version = "1.0",
                Normalized = true,
                CreatedAt = clock.GetUtcNow()
            };
            database.EmbeddingProfiles.Add(embProfile);
            await database.SaveChangesAsync(cancellationToken);
        }

        foreach (var item in chunkItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checkpoint = await database.IngestionChunkCheckpoints
                .FirstOrDefaultAsync(c => c.IngestionJobId == job.IngestionId && c.ChunkHash == item.ChunkHash && c.EmbeddingProfileId == job.EmbeddingProfileId, cancellationToken);

            if (checkpoint != null && checkpoint.State == "persisted") continue;

            if (checkpoint == null)
            {
                checkpoint = new IngestionChunkCheckpoint
                {
                    IngestionJobId = job.IngestionId,
                    ChunkHash = item.ChunkHash,
                    EmbeddingProfileId = job.EmbeddingProfileId,
                    State = "calling",
                    UpdatedAt = clock.GetUtcNow()
                };
                database.IngestionChunkCheckpoints.Add(checkpoint);
                await database.SaveChangesAsync(cancellationToken);
            }

            try
            {
                var embResult = await embeddingService.GenerateEmbeddingAsync(
                    item.Text,
                    job.EmbeddingProfileId,
                    ingestionJobId: job.IngestionId,
                    cancellationToken: cancellationToken);

                // Persiste ou obtém o KnowledgeChunk
                var chunk = await database.KnowledgeChunks
                    .FirstOrDefaultAsync(c => c.RevisionId == revision!.Id && c.ChunkHash == item.ChunkHash, cancellationToken);

                if (chunk == null)
                {
                    chunk = new KnowledgeChunk
                    {
                        Id = Guid.NewGuid(),
                        RevisionId = revision!.Id,
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

                var chunkVector = await database.ChunkVectors
                    .FirstOrDefaultAsync(v => v.ChunkId == chunk.Id && v.EmbeddingProfileId == job.EmbeddingProfileId, cancellationToken);

                if (chunkVector == null)
                {
                    chunkVector = new ChunkVector
                    {
                        ChunkId = chunk.Id,
                        EmbeddingProfileId = job.EmbeddingProfileId,
                        Vector = new Vector(embResult.Vector)
                    };
                    database.ChunkVectors.Add(chunkVector);
                }

                checkpoint.State = "persisted";
                checkpoint.CallAttemptId = embResult.AttemptId;
                checkpoint.UpdatedAt = clock.GetUtcNow();
                await database.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                checkpoint.State = "unknown";
                checkpoint.UpdatedAt = clock.GetUtcNow();
                await database.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }

        job.Status = "concluida";
        job.Stage = "publicacao";
        job.UpdatedAt = clock.GetUtcNow();

        var workItem = await database.IngestionWorkItems.FindAsync([job.IngestionId], cancellationToken);
        if (workItem != null) database.IngestionWorkItems.Remove(workItem);

        await database.SaveChangesAsync(cancellationToken);
    }
}

public sealed class IngestionValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class UnsupportedMediaTypeException(string message) : Exception(message);
