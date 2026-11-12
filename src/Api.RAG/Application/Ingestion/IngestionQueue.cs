using Api.RAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Application.Ingestion;

internal sealed record IngestionLease(Guid JobId, string Owner, long Token);

internal sealed class IngestionQueue(KnowledgeDbContext database, TimeProvider clock)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<IngestionLease?> ClaimAsync(string owner, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize claims across instances. This also keeps writes to shared chunks and profiles sequential.
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(40602002)", cancellationToken);

        var now = clock.GetUtcNow();
        if (await database.IngestionWorkItems.AnyAsync(item => item.LeaseExpiresAt > now, cancellationToken))
            return null;

        var item = await database.IngestionWorkItems.AsNoTracking()
            .Where(item => item.AvailableAt <= now && (item.LeaseExpiresAt == null || item.LeaseExpiresAt <= now))
            .OrderBy(item => item.AvailableAt).ThenBy(item => item.IngestionJobId)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null) return null;

        var token = item.FencingToken + 1;
        var claimed = await database.IngestionWorkItems
            .Where(work => work.IngestionJobId == item.IngestionJobId && work.FencingToken == item.FencingToken &&
                (work.LeaseExpiresAt == null || work.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(work => work.LeaseOwner, owner)
                .SetProperty(work => work.LeaseExpiresAt, now + LeaseDuration)
                .SetProperty(work => work.HeartbeatAt, now)
                .SetProperty(work => work.FencingToken, token), cancellationToken);
        if (claimed != 1) return null;

        await transaction.CommitAsync(cancellationToken);
        return new IngestionLease(item.IngestionJobId, owner, token);
    }

    public async Task SaveAsync(IngestionLease lease, CancellationToken cancellationToken, bool complete = false)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var updated = await OwnedBy(lease)
            .Where(item => item.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.HeartbeatAt, now)
                .SetProperty(item => item.LeaseExpiresAt, now + LeaseDuration), cancellationToken);
        if (updated != 1) throw new IngestionLeaseLostException();

        await database.SaveChangesAsync(cancellationToken);
        if (complete)
            await OwnedBy(lease).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task ReleaseAsync(IngestionLease lease, CancellationToken cancellationToken) =>
        OwnedBy(lease).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.LeaseOwner, (string?)null)
            .SetProperty(item => item.LeaseExpiresAt, (DateTimeOffset?)null), cancellationToken);

    private IQueryable<Api.RAG.Domain.Knowledge.IngestionWorkItem> OwnedBy(IngestionLease lease) =>
        database.IngestionWorkItems.Where(item => item.IngestionJobId == lease.JobId &&
            item.LeaseOwner == lease.Owner && item.FencingToken == lease.Token);
}

internal sealed class IngestionLeaseLostException : Exception { }
