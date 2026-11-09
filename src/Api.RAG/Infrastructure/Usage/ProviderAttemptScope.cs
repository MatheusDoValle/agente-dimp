namespace Api.RAG.Infrastructure.Usage;

public sealed class ProviderAttemptScope(ProviderCallAttemptStore store)
{
    public const int MaxTransientRetries = 3;
    public const int MaxTotalAttempts = 4;

    public async Task<T> ExecuteWithPolicyAsync<T>(
        Guid logicalCallId,
        Guid correlationId,
        Guid? executionId,
        Guid? ingestionJobId,
        ProviderOperation operation,
        string provider,
        string model,
        Func<int, AttemptReason, CancellationToken, Task<ProviderResponse<T>>> callDelegate,
        CancellationToken cancellationToken = default)
    {
        int totalAttempts = 0;

        while (true)
        {
            totalAttempts++;
            if (totalAttempts > MaxTotalAttempts)
                throw new InvalidOperationException("Limite global de tentativas de embedding excedido.");

            var reason = totalAttempts == 1 ? AttemptReason.Initial : AttemptReason.TransientRetry;

            var request = new ProviderCallRequest(
                LogicalCallId: logicalCallId,
                CorrelationId: correlationId,
                ExecutionId: executionId,
                IngestionJobId: ingestionJobId,
                Operation: operation,
                Provider: provider,
                Model: model,
                AttemptNumber: totalAttempts,
                Reason: reason);

            try
            {
                return await store.ExecuteRecordedAsync(request, async (attempt, ct) =>
                {
                    return await callDelegate(totalAttempts, reason, ct);
                }, cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex) && totalAttempts <= MaxTransientRetries)
            {
                continue;
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException;
}
