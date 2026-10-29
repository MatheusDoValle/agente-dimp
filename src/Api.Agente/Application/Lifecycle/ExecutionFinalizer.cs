using Api.Agente.Domain;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Application.Lifecycle;

public interface IExecutionMemoryCleanup
{
    Task<bool> DeleteAndConfirmAsync(Guid executionId, CancellationToken cancellationToken = default);
}

// US3 must replace this adapter when MongoDB context storage is activated.
public sealed class DisabledExecutionMemoryCleanup(IConfiguration configuration) : IExecutionMemoryCleanup
{
    public Task<bool> DeleteAndConfirmAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");
        if (configuration.GetValue<bool>("Memory:Enabled"))
            throw new InvalidOperationException("Limpeza MongoDB confirmada obrigatoria quando memoria estiver habilitada.");
        return Task.FromResult(true);
    }
}

public sealed record FinalizationResult(bool Ended, string? LimitationCode);

public sealed class ExecutionFinalizer(AgentDbContext database, ITemporaryArtifactStore artifacts,
    IExecutionMemoryCleanup memory, TimeProvider clock, ILoggerFactory? loggerFactory = null)
{
    public async Task<FinalizationResult> FinalizeAsync(Guid executionId, ExecutionState terminal, CancellationToken cancellationToken = default)
    {
        if (!Execution.IsTerminal(terminal)) throw new ArgumentException("Estado final invalido.");
        var execution = await database.Executions.SingleAsync(x => x.Id == executionId, cancellationToken);
        if (execution.EndedAt.HasValue) return new FinalizationResult(true, null);
        execution.BeginCleanup(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);

        bool cleaned;
        try
        {
            cleaned = await artifacts.DeleteExecutionAsync(executionId, cancellationToken)
                && await memory.DeleteAndConfirmAsync(executionId, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException)
        {
            cleaned = false;
        }

        if (!cleaned)
        {
            execution.CleanupFailed();
            await database.SaveChangesAsync(CancellationToken.None);
            loggerFactory?.CreateLogger("AgenteDIMP.Safe.Lifecycle").LogWarning(
                "CleanupUnconfirmed {ExecutionId} {CorrelationId}", execution.Id, execution.CorrelationId);
            return new FinalizationResult(false, "CLEANUP_UNCONFIRMED");
        }

        var report = await database.Reports.SingleOrDefaultAsync(x => x.ExecutionId == executionId, cancellationToken);
        execution.CompleteAfterCleanup(terminal, clock.GetUtcNow(), report is not null);
        report?.SetExpiration(execution.EndedAt!.Value);
        await database.SaveChangesAsync(cancellationToken);
        return new FinalizationResult(true, null);
    }
}
