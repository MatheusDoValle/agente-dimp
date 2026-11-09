using Api.RAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.RAG.Workers;

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
                var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
                var now = clock.GetUtcNow();
                var threshold = now.AddDays(-90);

                var expiredAttempts = await db.ProviderCallAttempts
                    .Where(a => a.StartedAt < threshold)
                    .ToListAsync(stoppingToken);

                if (expiredAttempts.Count > 0)
                {
                    db.ProviderCallAttempts.RemoveRange(expiredAttempts);
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Expurgadas {Count} tentativas de embedding com mais de 90 dias.", expiredAttempts.Count);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro no expurgo de tentativas de embedding.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
