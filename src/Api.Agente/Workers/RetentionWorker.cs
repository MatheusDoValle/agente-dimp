using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Workers;

public sealed class RetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                var expirationService = new ReportExpirationService(db);
                var now = clock.GetUtcNow();

                var expiredCount = await expirationService.ExpireEligibleReportsAsync(now, stoppingToken);
                if (expiredCount > 0)
                {
                    logger.LogInformation("Expirados {Count} relatorios com mais de 30 dias.", expiredCount);
                }

                var purgedCount = await expirationService.PurgeExpiredTombstonesAsync(now, stoppingToken);
                if (purgedCount > 0)
                {
                    logger.LogInformation("Expurgados {Count} tombstones com mais de 90 dias de expiracao.", purgedCount);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro no ciclo do RetentionWorker.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
