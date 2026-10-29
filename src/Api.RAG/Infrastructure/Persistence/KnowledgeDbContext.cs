using Api.RAG.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Infrastructure.Persistence;

public sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : DbContext(options)
{
    public DbSet<ProviderCallAttempt> ProviderCallAttempts => Set<ProviderCallAttempt>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("rag");
        model.HasPostgresExtension("vector");
        model.Entity<ProviderCallAttempt>(b =>
        {
            b.ToTable("provider_call_attempts"); b.HasKey(x => x.AttemptId);
            b.Property(x => x.Operation).HasConversion<string>();
            b.Property(x => x.Reason).HasConversion<string>();
            b.Property(x => x.Status).HasConversion<string>();
            b.Property(x => x.UsageState).HasConversion<string>();
            b.Property(x => x.EstimatedCost).HasPrecision(28, 12);
            b.HasIndex(x => new { x.ExecutionId, x.StartedAt });
            b.HasIndex(x => new { x.IngestionJobId, x.StartedAt });
            b.HasIndex(x => new { x.LogicalCallId, x.AttemptNumber }).IsUnique();
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateMetadata();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateMetadata();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    private void ValidateMetadata()
    {
        foreach (var entry in ChangeTracker.Entries<ProviderCallAttempt>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Validate();
    }
}
