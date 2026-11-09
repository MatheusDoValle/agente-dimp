using Api.RAG.Infrastructure.Persistence;
using Api.RAG.Infrastructure.Retrieval;
using Api.RAG.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Infrastructure.Mcp;

public static class UsageTools
{
    public static async Task<RetrievalUsageEnvelope> GetIngestionUsageEnvelopeAsync(
        Guid ingestionId,
        KnowledgeDbContext db,
        CancellationToken ct = default)
    {
        var attempts = await db.ProviderCallAttempts.AsNoTracking()
            .Where(a => a.IngestionJobId == ingestionId)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

        var references = attempts.Select(a => new RetrievalUsageReference(
            a.AttemptId,
            "Api.RAG",
            a.UsageState.ToString().ToLowerInvariant(),
            a.TokensInput,
            a.TokensOutput,
            a.CachedTokens,
            a.EmbeddingTokens)).ToList();

        var hasUnknown = attempts.Any(a => a.UsageState == UsageState.Unknown || a.UsageState == UsageState.Partial);

        return new RetrievalUsageEnvelope(
            ingestionId,
            references,
            hasUnknown);
    }
}
