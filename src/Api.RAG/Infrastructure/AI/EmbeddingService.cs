using Api.RAG.Infrastructure.Development;
using Api.RAG.Infrastructure.Usage;
using OpenAI.Embeddings;

namespace Api.RAG.Infrastructure.AI;

public sealed record EmbeddingResult(string ProfileId, float[] Vector, Guid? AttemptId);

public interface IEmbeddingService
{
    Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string profileId,
        Guid? ingestionJobId = null,
        Guid? executionId = null,
        CancellationToken cancellationToken = default);
}

public sealed class EmbeddingService(
    ProviderCallAttemptStore attemptStore,
    OfflineEmbeddingProvider offlineProvider,
    IConfiguration configuration) : IEmbeddingService
{
    public const string DefaultBaselineProfileId = "text-embedding-3-small-1536";
    public const string OfflineProfileId = OfflineEmbeddingProvider.ProfileId;

    public async Task<EmbeddingResult> GenerateEmbeddingAsync(
        string text,
        string profileId,
        Guid? ingestionJobId = null,
        Guid? executionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();

        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        var isOffline = string.IsNullOrWhiteSpace(apiKey) || profileId == OfflineProfileId || configuration["DIMP_TEST_MODE"] == "compose-local";

        var isIngestion = ingestionJobId.HasValue;
        var operation = isIngestion ? ProviderOperation.IngestionEmbedding : ProviderOperation.QueryEmbedding;

        // Se for ingestão mas IngestionJobId for nulo, gera um para o escopo
        Guid? effectiveIngestionJobId = isIngestion ? ingestionJobId : null;
        Guid? effectiveExecutionId = !isIngestion ? (executionId ?? Guid.NewGuid()) : null;

        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: effectiveExecutionId,
            IngestionJobId: effectiveIngestionJobId,
            Operation: operation,
            Provider: isOffline ? "offline" : "openai",
            Model: isOffline ? "fake-offline-embedding" : "text-embedding-3-small",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        return await attemptStore.ExecuteRecordedAsync(request, async (attempt, ct) =>
        {
            if (isOffline)
            {
                var fakeResult = await offlineProvider.GenerateAsync(text, ct);
                var simulatedUsage = new UsageMeasurement(
                    InputTokens: null,
                    OutputTokens: null,
                    CachedTokens: null,
                    EmbeddingTokens: Math.Max(1, text.Length / 4));

                return new ProviderResponse<EmbeddingResult>(
                    Value: new EmbeddingResult(profileId, fakeResult.Values, attempt.AttemptId),
                    Usage: simulatedUsage,
                    ReturnedModel: "fake-offline-embedding",
                    ProviderRequestId: "offline-" + Guid.NewGuid().ToString("N"));
            }

            var client = new EmbeddingClient("text-embedding-3-small", apiKey);
            var embedding = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
            var vector = embedding.Value.ToFloats().ToArray();

            var usage = new UsageMeasurement(
                InputTokens: null,
                OutputTokens: null,
                CachedTokens: null,
                EmbeddingTokens: null);

            return new ProviderResponse<EmbeddingResult>(
                Value: new EmbeddingResult(profileId, vector, attempt.AttemptId),
                Usage: usage,
                ReturnedModel: "text-embedding-3-small",
                ProviderRequestId: null);
        }, cancellationToken);
    }
}
