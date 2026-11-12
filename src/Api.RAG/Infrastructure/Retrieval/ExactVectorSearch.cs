using System.Text.RegularExpressions;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.AI;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    double RankingMetric, bool IsLexicalMatch = false);

public sealed record SearchResult(
    Guid RetrievalId,
    IReadOnlyList<SearchResultHit> Hits,
    IReadOnlyList<string> LimitationCodes,
    Guid? AttemptId);

public sealed class ExactVectorSearch(
    KnowledgeDbContext database,
    IEmbeddingService embeddingService,
    IOptions<RagOptions>? options = null)
{
    private sealed class RankedChunk
    {
        public required KnowledgeChunk Chunk { get; init; }
        public required DocumentRevision Revision { get; init; }
        public required double Distance { get; init; }
    }

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

        var normalizedSha = documentRevisionSha256.Trim().ToLowerInvariant();
        var query = from vector in database.PublishedVectors().AsNoTracking()
                    join chunk in database.KnowledgeChunks.AsNoTracking() on vector.ChunkId equals chunk.Id
                    join revision in database.DocumentRevisions.AsNoTracking() on chunk.RevisionId equals revision.Id
                    where revision.RevisionSha256 == normalizedSha
                          && revision.LayoutVersion == layoutVersion
                          && vector.EmbeddingProfileId == embeddingProfileId
                    select new { Vector = vector.Vector, Chunk = chunk, Revision = revision };

        if (recordCodes != null && recordCodes.Count > 0)
        {
            query = query.Where(x => x.Chunk.RecordCode == null || recordCodes.Contains(x.Chunk.RecordCode));
        }

        if (!await query.AnyAsync(cancellationToken))
            return new SearchResult(Guid.NewGuid(), [], ["NO_EVIDENCE_FOUND"], null);

        var terms = embeddingProfileId == EmbeddingService.DefaultBaselineProfileId
            ? DimpSearchVocabulary.Match(queryText, recordCodes)
            : [];
        var embeddingText = terms.Length == 0
            ? queryText
            : queryText + "\n" + string.Join("\n", terms.Select(term => term.SearchContext));
        var queryEmbResult = await embeddingService.GenerateEmbeddingAsync(
            embeddingText, embeddingProfileId, executionId: executionId, cancellationToken: cancellationToken);
        var queryVector = new Vector(queryEmbResult.Vector);

        var ranked = query.Select(x => new RankedChunk
        {
            Chunk = x.Chunk,
            Revision = x.Revision,
            Distance = x.Vector.CosineDistance(queryVector)
        });
        var semanticCandidates = options?.Value.MaximumCosineDistance is double maximumDistance
            ? ranked.Where(x => x.Distance <= maximumDistance)
            : ranked;

        var semanticHits = await semanticCandidates.OrderBy(x => x.Distance).ThenBy(x => x.Chunk.Id)
            .Take(topK)
            .ToListAsync(cancellationToken);

        // Exact matches require both a recognized question and literal evidence in the indexed manual.
        var lexicalHits = new List<RankedChunk>();
        foreach (var evidencePattern in terms.SelectMany(term => term.EvidencePatterns))
        {
            var lexicalHit = await ranked
                .Where(x => Regex.IsMatch(x.Chunk.Text, evidencePattern, RegexOptions.IgnoreCase))
                .OrderBy(x => x.Distance).ThenBy(x => x.Chunk.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (lexicalHit is not null)
                lexicalHits.Add(lexicalHit);
        }

        var lexicalChunkIds = lexicalHits.Select(x => x.Chunk.Id).ToHashSet();
        var results = lexicalHits.Concat(semanticHits).DistinctBy(x => x.Chunk.Id)
            .Take(topK).OrderBy(x => x.Distance);
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
            RankingMetric: r.Distance, IsLexicalMatch: lexicalChunkIds.Contains(r.Chunk.Id))).ToList();

        var limitations = hits.Count == 0 ? new[] { "NO_EVIDENCE_FOUND" } : Array.Empty<string>();

        return new SearchResult(
            RetrievalId: Guid.NewGuid(),
            Hits: hits,
            LimitationCodes: limitations,
            AttemptId: queryEmbResult.AttemptId);
    }
}
