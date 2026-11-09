using Api.Agente.Application.Lifecycle;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.Jobs;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Workers;

public sealed class ArtifactRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ArtifactRecoveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                var artifacts = scope.ServiceProvider.GetRequiredService<ITemporaryArtifactStore>();
                var memory = scope.ServiceProvider.GetRequiredService<IExecutionMemoryCleanup>();
                var now = clock.GetUtcNow();

                // 1. Recover stale or crashed processing jobs
                var staleJobs = await db.ProcessingJobs
                    .Where(j => j.Status == JobReservationStatus.Leased && j.LeaseExpiresAt < now)
                    .ToListAsync(stoppingToken);

                foreach (var job in staleJobs)
                {
                    logger.LogWarning("Job {ExecutionId} com lease expirado detectado. Tentando recuperacao.", job.ExecutionId);
                    if (job.AttemptNumber < 3)
                    {
                        job.Status = JobReservationStatus.Pending;
                        job.AvailableAt = now;
                        job.LeaseOwner = null;
                        job.LeaseExpiresAt = null;
                    }
                    else
                    {
                        job.Status = JobReservationStatus.Completed;
                        var execution = await db.Executions.SingleOrDefaultAsync(e => e.Id == job.ExecutionId, stoppingToken);
                        if (execution != null && !execution.EndedAt.HasValue)
                        {
                            execution.BeginCleanup(now);
                            await artifacts.DeleteExecutionAsync(execution.Id, stoppingToken);
                            await memory.DeleteAndConfirmAsync(execution.Id, stoppingToken);
                            execution.CompleteAfterCleanup(ExecutionState.Failed, now, hasReport: false);
                        }
                    }
                }

                if (staleJobs.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }

                // 2. Scan for orphaned temporary files belonging to ended executions
                var endedExecutions = await db.Executions.AsNoTracking()
                    .Where(e => e.EndedAt.HasValue)
                    .Select(e => e.Id)
                    .ToListAsync(stoppingToken);

                foreach (var execId in endedExecutions)
                {
                    await artifacts.DeleteExecutionAsync(execId, stoppingToken);
                    await memory.DeleteAndConfirmAsync(execId, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro no ciclo do ArtifactRecoveryWorker.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
