namespace Api.Agente.Infrastructure.Usage;

public sealed class ProviderAttemptScope(
    ProviderCallAttemptStore store,
    IPriceBookCostEstimator costEstimator)
{
    public const int MaxTransientRetries = 3;
    public const int MaxRepairs = 1;
    public const int MaxTotalAttempts = 4;

    public async Task<T> ExecuteWithPolicyAsync<T>(
        Guid logicalCallId,
        Guid correlationId,
        Guid? executionId,
        ProviderOperation operation,
        string provider,
        string model,
        Func<int, AttemptReason, CancellationToken, Task<ProviderResponse<T>>> callDelegate,
        Func<T, bool>? semanticValidator = null,
        CancellationToken cancellationToken = default)
    {
        int transientAttempts = 0;
        int repairAttempts = 0;
        int totalAttempts = 0;

        while (true)
        {
            totalAttempts++;
            if (totalAttempts > MaxTotalAttempts)
            {
                throw new InvalidOperationException("Limite global de tentativas do provedor excedido.");
            }

            var reason = totalAttempts == 1 ? AttemptReason.Initial
                : repairAttempts > 0 ? AttemptReason.SemanticRepair
                : AttemptReason.TransientRetry;

            var request = new ProviderCallRequest(
                LogicalCallId: logicalCallId,
                CorrelationId: correlationId,
                ExecutionId: executionId,
                IngestionJobId: null,
                Operation: operation,
                Provider: provider,
                Model: model,
                AttemptNumber: totalAttempts,
                Reason: reason);

            try
            {
                var result = await store.ExecuteRecordedAsync(request, async (attempt, ct) =>
                {
                    var response = await callDelegate(totalAttempts, reason, ct);
                    var cost = costEstimator.EstimateCost(provider, model, operation, response.Usage);
                    attempt.ApplyCostEstimate(cost);
                    return response;
                }, cancellationToken);

                if (semanticValidator != null && !semanticValidator(result))
                {
                    if (repairAttempts < MaxRepairs && totalAttempts < MaxTotalAttempts)
                    {
                        repairAttempts++;
                        continue;
                    }
                    throw new InvalidOperationException("Resposta semantica invalida e reparos esgotados.");
                }

                return result;
            }
            catch (Exception ex) when (IsTransient(ex) && transientAttempts < MaxTransientRetries && totalAttempts < MaxTotalAttempts)
            {
                transientAttempts++;
                continue;
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TimeoutException or TaskCanceledException;
}
