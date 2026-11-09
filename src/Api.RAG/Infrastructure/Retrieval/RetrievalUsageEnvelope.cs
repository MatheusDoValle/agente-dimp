namespace Api.RAG.Infrastructure.Retrieval;

public sealed record RetrievalUsageReference(
    Guid CallAttemptId,
    string ServiceOwner,
    string UsageState,
    long? TokensInput,
    long? TokensOutput,
    long? CachedTokens,
    long? EmbeddingTokens);

public sealed record RetrievalUsageEnvelope(
    Guid? RetrievalId,
    IReadOnlyList<RetrievalUsageReference> UsageReferences,
    bool HasUnknownUsage);
