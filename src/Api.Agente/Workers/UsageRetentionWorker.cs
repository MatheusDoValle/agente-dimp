using Api.Agente.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Workers;

public sealed class UsageRetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<UsageRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
                var now = clock.GetUtcNow();
                var threshold = now.AddDays(-90);

                var expiredAttempts = await db.ProviderCallAttempts
                    .Where(a => a.StartedAt < threshold)
                    .ToListAsync(stoppingToken);

                if (expiredAttempts.Count > 0)
                {
                    db.ProviderCallAttempts.RemoveRange(expiredAttempts);
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Expurgadas {Count} tentativas de provedor com mais de 90 dias.", expiredAttempts.Count);
                }

                var referencedPriceBookIds = await db.ProviderCallAttempts
                    .Where(a => a.PriceBookVersionId.HasValue)
                    .Select(a => a.PriceBookVersionId!.Value)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                var unreferencedOldPriceBooks = await db.PriceBooks
                    .Where(p => p.QueriedAt < threshold && !referencedPriceBookIds.Contains(p.Id))
                    .ToListAsync(stoppingToken);

                if (unreferencedOldPriceBooks.Count > 0)
                {
                    db.PriceBooks.RemoveRange(unreferencedOldPriceBooks);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro no expurgo de tentativas de provedor.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
