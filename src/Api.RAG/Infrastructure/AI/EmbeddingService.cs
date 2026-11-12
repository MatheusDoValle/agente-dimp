using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using Api.RAG.Infrastructure.Development;
using OpenAI;
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
    internal const string OpenAiModel = "text-embedding-3-small";
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
        var isOffline = profileId == OfflineProfileId;
        if (!isOffline)
        {
            if (profileId != DefaultBaselineProfileId ||
                !configuration.GetValue<bool>("DevelopmentProfile:ExternalCallsEnabled") ||
                configuration["DIMP_TEST_MODE"] == "compose-local" || string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Perfil de embedding indisponível neste ambiente.");

            if (configuration.GetValue<int>("Processing:MaximumProviderCalls") < 1 ||
                Encoding.UTF8.GetByteCount(text) > configuration.GetValue<int>("Processing:TokenBudget"))
                throw new InvalidOperationException("Orçamento de embeddings excedido.");
        }

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
            Model: isOffline ? "fake-offline-embedding" : OpenAiModel,
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

            var client = new EmbeddingClient(OpenAiModel, new ApiKeyCredential(apiKey!), new OpenAIClientOptions
            {
                RetryPolicy = new ClientRetryPolicy(0)
            });
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue<double>("Processing:ProviderTimeoutSeconds")));
            OpenAIEmbedding embedding;
            try
            {
                embedding = (await client.GenerateEmbeddingAsync(text, cancellationToken: timeout.Token)).Value;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException("Prazo do provedor de embeddings excedido.");
            }

            var vector = embedding.ToFloats().ToArray();
            if (vector.Length != 1536)
                throw new InvalidOperationException("Dimensão do embedding incompatível com o perfil.");

            var usage = new UsageMeasurement(
                InputTokens: null,
                OutputTokens: null,
                CachedTokens: null,
                EmbeddingTokens: null);

            return new ProviderResponse<EmbeddingResult>(
                Value: new EmbeddingResult(profileId, vector, attempt.AttemptId),
                Usage: usage,
                ReturnedModel: OpenAiModel,
                ProviderRequestId: null);
        }, cancellationToken);
    }
}
