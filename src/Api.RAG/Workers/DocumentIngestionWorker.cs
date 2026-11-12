using Api.RAG.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.RAG.Workers;

internal sealed class DocumentIngestionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<DocumentIngestionWorker> logger) : BackgroundService
{
    private readonly string owner = Guid.NewGuid().ToString("N");
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queue = scope.ServiceProvider.GetRequiredService<IngestionQueue>();
                var lease = await queue.ClaimAsync(owner, stoppingToken);
                if (lease is not null)
                {
                    await ProcessAsync(scope.ServiceProvider, lease, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (IngestionLeaseLostException)
            {
                logger.LogWarning("Lease da ingestão expirou. A fila pode retomar o trabalho.");
            }
            catch (Exception ex)
            {
                logger.LogWarning("Não foi possível executar a fila de ingestão. Tipo: {ErrorType}.", ex.GetType().Name);
            }

            await Task.Delay(PollInterval, clock, stoppingToken);
        }
    }

    private async Task ProcessAsync(IServiceProvider services, IngestionLease lease, CancellationToken stoppingToken)
    {
        try
        {
            await services.GetRequiredService<DocumentIngestionService>().ProcessJobAsync(lease, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await using var recovery = scopeFactory.CreateAsyncScope();
            await recovery.ServiceProvider.GetRequiredService<IngestionQueue>().ReleaseAsync(lease, CancellationToken.None);
            throw;
        }
        catch (OperationCanceledException)
        {
            await RecordFailureAsync(lease, "INGESTION_DEADLINE_EXCEEDED", stoppingToken);
        }
        catch (IngestionValidationException ex)
        {
            await RecordFailureAsync(lease, ex.Code, stoppingToken);
        }
    }

    private async Task RecordFailureAsync(IngestionLease lease, string code, CancellationToken stoppingToken)
    {
        await using var recovery = scopeFactory.CreateAsyncScope();
        await recovery.ServiceProvider.GetRequiredService<DocumentIngestionService>()
            .FailJobAsync(lease, code, stoppingToken);
        logger.LogWarning("Ingestão {JobId} falhou com código {Code}.", lease.JobId, code);
    }
}
