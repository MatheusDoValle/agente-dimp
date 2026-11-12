using Api.RAG.Infrastructure.AI;
using Api.RAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Api.RAG.Infrastructure.Retrieval;

public sealed record SearchResultHit(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentRevisionSha256,
    string LayoutVersion,
    string EmbeddingProfileId,
    int[] Pages,
    string Section,
    IReadOnlyList<string> RecordCodes,
    IReadOnlyList<string> FieldCodes,
    string Text,
    double RankingMetric);

public sealed record SearchResult(
    Guid RetrievalId,
    IReadOnlyList<SearchResultHit> Hits,
    IReadOnlyList<string> LimitationCodes,
    Guid? AttemptId);

public sealed class ExactVectorSearch(
    KnowledgeDbContext database,
    IEmbeddingService embeddingService)
{
    public async Task<SearchResult> SearchAsync(
        string queryText,
        string layoutVersion,
        string documentRevisionSha256,
        string embeddingProfileId,
        IReadOnlyList<string>? recordCodes = null,
        int topK = 5,
        Guid? executionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentRevisionSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingProfileId);
        cancellationToken.ThrowIfCancellationRequested();

        if (layoutVersion != "10")
        {
            return new SearchResult(
                RetrievalId: Guid.NewGuid(),
                Hits: [],
                LimitationCodes: ["LAYOUT_VERSION_NOT_SUPPORTED"],
                AttemptId: null);
        }

        // Gera embedding da query
        var queryEmbResult = await embeddingService.GenerateEmbeddingAsync(
            queryText,
            embeddingProfileId,
            executionId: executionId,
            cancellationToken: cancellationToken);

        var queryVector = new Vector(queryEmbResult.Vector);
        var normalizedSha = documentRevisionSha256.Trim().ToLowerInvariant();

        // Monta consulta exata pgvector com filtros estritos de revisão e perfil
        var query = from cv in database.PublishedVectors().AsNoTracking()
                    join chunk in database.KnowledgeChunks.AsNoTracking() on cv.ChunkId equals chunk.Id
                    join rev in database.DocumentRevisions.AsNoTracking() on chunk.RevisionId equals rev.Id
                    where rev.RevisionSha256 == normalizedSha
                          && rev.LayoutVersion == layoutVersion
                          && cv.EmbeddingProfileId == embeddingProfileId
                    select new
                    {
                        Chunk = chunk,
                        Revision = rev,
                        Distance = cv.Vector.CosineDistance(queryVector)
                    };

        if (recordCodes != null && recordCodes.Count > 0)
        {
            query = query.Where(x => x.Chunk.RecordCode == null || recordCodes.Contains(x.Chunk.RecordCode));
        }

        var results = await query
            .OrderBy(x => x.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        var hits = results.Select(r => new SearchResultHit(
            ChunkId: r.Chunk.Id,
            DocumentId: r.Revision.DocumentId,
            DocumentRevisionSha256: r.Revision.RevisionSha256,
            LayoutVersion: r.Revision.LayoutVersion,
            EmbeddingProfileId: embeddingProfileId,
            Pages: r.Chunk.Pages,
            Section: r.Chunk.Section,
            RecordCodes: string.IsNullOrWhiteSpace(r.Chunk.RecordCode) ? [] : [r.Chunk.RecordCode],
            FieldCodes: string.IsNullOrWhiteSpace(r.Chunk.FieldCode) ? [] : [r.Chunk.FieldCode],
            Text: r.Chunk.Text,
            RankingMetric: r.Distance)).ToList();

        var limitations = hits.Count == 0 ? new[] { "NO_EVIDENCE_FOUND" } : Array.Empty<string>();

        return new SearchResult(
            RetrievalId: Guid.NewGuid(),
            Hits: hits,
            LimitationCodes: limitations,
            AttemptId: queryEmbResult.AttemptId);
    }
}
