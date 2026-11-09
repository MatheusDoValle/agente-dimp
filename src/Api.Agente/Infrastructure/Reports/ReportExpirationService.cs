using Api.Agente.Domain.Reports;
using Api.Agente.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Infrastructure.Reports;

public sealed class ReportExpirationService(AgentDbContext dbContext)
{
    public async Task<int> ExpireEligibleReportsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var expiredExecutions = await dbContext.Executions
            .Where(e => e.ReportExpiresAt.HasValue && e.ReportExpiresAt.Value <= now)
            .ToListAsync(cancellationToken);

        int count = 0;
        foreach (var exec in expiredExecutions)
        {
            var expiredAt = exec.ReportExpiresAt!.Value;
            var purgeAt = expiredAt.AddDays(90);

            var existingTombstone = await dbContext.ExecutionTombstones
                .SingleOrDefaultAsync(t => t.ExecutionId == exec.Id, cancellationToken);

            if (existingTombstone is null)
            {
                dbContext.ExecutionTombstones.Add(new ExecutionTombstone(exec.Id, expiredAt, purgeAt));
            }

            var reports = dbContext.Reports.Where(r => r.ExecutionId == exec.Id);
            dbContext.Reports.RemoveRange(reports);

            var findings = dbContext.Findings.Where(f => f.ExecutionId == exec.Id);
            dbContext.Findings.RemoveRange(findings);

            var evidence = dbContext.Evidence.Where(e => e.ExecutionId == exec.Id);
            dbContext.Evidence.RemoveRange(evidence);

            var evaluations = dbContext.RuleEvaluations.Where(r => r.ExecutionId == exec.Id);
            dbContext.RuleEvaluations.RemoveRange(evaluations);

            dbContext.Executions.Remove(exec);
            count++;
        }

        if (count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    public async Task<int> PurgeExpiredTombstonesAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var purgedTombstones = await dbContext.ExecutionTombstones
            .Where(t => t.PurgeAt <= now)
            .ToListAsync(cancellationToken);

        if (purgedTombstones.Count > 0)
        {
            dbContext.ExecutionTombstones.RemoveRange(purgedTombstones);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return purgedTombstones.Count;
    }
}
