using Api.Agente.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Infrastructure.Usage;

public sealed class ProviderCallAttemptStore(AgentDbContext database, TimeProvider clock)
{
    public async Task<ProviderCallAttempt> BeginAsync(ProviderCallRequest request, CancellationToken cancellationToken = default)
    {
        var attempt = new ProviderCallAttempt(request, clock.GetUtcNow());
        database.ProviderCallAttempts.Add(attempt);
        await database.SaveChangesAsync(cancellationToken);
        return attempt;
    }

    public async Task CompleteAsync(ProviderCallAttempt attempt, UsageMeasurement usage, string? returnedModel = null,
        string? providerRequestId = null, CancellationToken cancellationToken = default)
    {
        attempt.Complete(usage, clock.GetUtcNow(), returnedModel, providerRequestId);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> ExecuteRecordedAsync<T>(ProviderCallRequest request,
        Func<ProviderCallAttempt, CancellationToken, Task<ProviderResponse<T>>> invoke, CancellationToken cancellationToken = default)
    {
        // A failed BeginAsync never reaches the provider delegate.
        var attempt = await BeginAsync(request, cancellationToken);
        try
        {
            var response = await invoke(attempt, cancellationToken);
            await CompleteAsync(attempt, response.Usage, response.ReturnedModel, response.ProviderRequestId, cancellationToken);
            return response.Value;
        }
        catch
        {
            if (attempt.Status == AttemptStatus.Calling)
            {
                attempt.Fail(clock.GetUtcNow(), cancellationToken.IsCancellationRequested);
                try { await database.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateException) { /* Pre-call record stays calling/unknown for reconciliation. */ }
            }
            throw;
        }
    }
}
