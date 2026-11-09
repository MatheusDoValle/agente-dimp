using Api.Agente.Application.Privacy;
using Api.Agente.Domain;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Jobs;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Infrastructure.Persistence;

public sealed class AgentDbContext(DbContextOptions<AgentDbContext> options, ContentSanitizer? sanitizer = null) : DbContext(options)
{
    public DbSet<Execution> Executions => Set<Execution>();
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<EvidenceSnapshot> Evidence => Set<EvidenceSnapshot>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<RuleEvaluation> RuleEvaluations => Set<RuleEvaluation>();
    public DbSet<ProviderCallAttempt> ProviderCallAttempts => Set<ProviderCallAttempt>();
    public DbSet<ExecutionTombstone> ExecutionTombstones => Set<ExecutionTombstone>();
    public DbSet<PriceBookVersion> PriceBooks => Set<PriceBookVersion>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("agente");
        model.Entity<Execution>(b =>
        {
            b.ToTable("executions"); b.HasKey(x => x.Id);
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Status).HasConversion<string>();
            b.Property(x => x.Stage).HasConversion<string>();
            b.Property(x => x.Verdict).HasConversion<string>();
            b.HasIndex(x => x.CorrelationId).IsUnique();
        });
        model.Entity<ProcessingJob>(b =>
        {
            b.ToTable("processing_jobs"); b.HasKey(x => x.ExecutionId);
            b.HasOne<Execution>().WithOne().HasForeignKey<ProcessingJob>(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Status).HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<JobReservationStatus>(v, true));
            b.HasIndex(x => new { x.Status, x.AvailableAt, x.LeaseExpiresAt });
        });
        model.Entity<Report>(b =>
        {
            b.ToTable("reports"); b.HasKey(x => x.ExecutionId);
            b.HasOne<Execution>().WithOne().HasForeignKey<Report>(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.AnalyticContent).HasColumnType("jsonb");
            b.HasIndex(x => x.ExpiresAt);
        });
        model.Entity<EvidenceSnapshot>(b =>
        {
            b.ToTable("evidence"); b.HasKey(x => x.Id);
            b.HasOne<Execution>().WithMany().HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Source).HasConversion<string>();
            b.HasIndex(x => new { x.ExecutionId, x.Id }).IsUnique();
        });
        model.Entity<Finding>(b =>
        {
            b.ToTable("findings"); b.HasKey(x => x.Id);
            b.HasOne<Execution>().WithMany().HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Category).HasConversion<string>();
            b.Property(x => x.Severity).HasConversion<string>();
            b.Property(x => x.Origin).HasConversion<string>();
            b.OwnsOne(x => x.Location);
            b.HasIndex(x => new { x.ExecutionId, x.Id });
        });
        model.Entity<RuleEvaluation>(b =>
        {
            b.ToTable("rule_evaluations"); b.HasKey(x => x.Id);
            b.HasOne<Execution>().WithMany().HasForeignKey(x => x.ExecutionId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Result).HasConversion<string>();
            b.Property(x => x.Applicability).HasConversion<string>();
            b.HasIndex(x => new { x.ExecutionId, x.RuleId, x.OccurrenceKey }).IsUnique();
        });
        model.Entity<ProviderCallAttempt>(b =>
        {
            b.ToTable("provider_call_attempts"); b.HasKey(x => x.AttemptId);
            b.Property(x => x.Operation).HasConversion<string>();
            b.Property(x => x.Reason).HasConversion<string>();
            b.Property(x => x.Status).HasConversion<string>();
            b.Property(x => x.UsageState).HasConversion<string>();
            b.Property(x => x.EstimatedCost).HasPrecision(28, 12);
            b.HasIndex(x => new { x.ExecutionId, x.StartedAt });
            b.HasIndex(x => new { x.LogicalCallId, x.AttemptNumber }).IsUnique();
            // No FK cascade: usage outlives reports.
        });
        model.Entity<ExecutionTombstone>(b =>
        {
            b.ToTable("execution_tombstones");
            b.HasKey(x => x.ExecutionId);
            b.HasIndex(x => x.PurgeAt);
        });
        model.Entity<PriceBookVersion>(b =>
        {
            b.ToTable("price_books");
            b.HasKey(x => x.Id);
            b.OwnsMany(x => x.Rates, rb =>
            {
                rb.ToTable("price_book_rates");
                rb.WithOwner().HasForeignKey("PriceBookId");
                rb.Property(r => r.InputPricePerMillion).HasPrecision(28, 12);
                rb.Property(r => r.OutputPricePerMillion).HasPrecision(28, 12);
                rb.Property(r => r.CachedPricePerMillion).HasPrecision(28, 12);
                rb.Property(r => r.EmbeddingPricePerMillion).HasPrecision(28, 12);
            });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidatePayloads();
        ValidateEvidenceOwnership();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidatePayloads();
        await ValidateEvidenceOwnershipAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidatePayloads()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<IMaskedPayload>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var field in entry.Entity.GetMaskedFields())
            {
                if (entry.State == EntityState.Modified && !entry.Property(field.Key).IsModified) continue;
                if (sanitizer is null) throw new SensitiveContentException();
                sanitizer.AssertSafe(entry.Entity.ExecutionId, field.Value);
            }
        }
        foreach (var entry in ChangeTracker.Entries<RuleEvaluation>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.ValidateMetadata();
        foreach (var entry in ChangeTracker.Entries<ProviderCallAttempt>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Validate();
    }

    private void ValidateEvidenceOwnership()
    {
        foreach (var finding in ChangedFindings())
            foreach (var id in finding.EvidenceIds)
                if (TrackedEvidenceInvalid(id, finding.ExecutionId) || !PendingEvidenceBelongs(id, finding.ExecutionId)
                    && !Evidence.Any(e => e.Id == id && e.ExecutionId == finding.ExecutionId))
                    throw new InvalidOperationException("Evidencia ausente ou de outra execucao.");
    }

    private async Task ValidateEvidenceOwnershipAsync(CancellationToken cancellationToken)
    {
        foreach (var finding in ChangedFindings())
            foreach (var id in finding.EvidenceIds)
                if (TrackedEvidenceInvalid(id, finding.ExecutionId) || !PendingEvidenceBelongs(id, finding.ExecutionId)
                    && !await Evidence.AnyAsync(e => e.Id == id && e.ExecutionId == finding.ExecutionId, cancellationToken))
                    throw new InvalidOperationException("Evidencia ausente ou de outra execucao.");
    }

    private IEnumerable<Finding> ChangedFindings() => ChangeTracker.Entries<Finding>()
        .Where(e => e.State is EntityState.Added or EntityState.Modified).Select(e => e.Entity);
    private bool TrackedEvidenceInvalid(Guid id, Guid executionId) => ChangeTracker.Entries<EvidenceSnapshot>()
        .Any(e => e.Entity.Id == id && (e.State == EntityState.Deleted || e.Entity.ExecutionId != executionId));
    private bool PendingEvidenceBelongs(Guid id, Guid executionId) => ChangeTracker.Entries<EvidenceSnapshot>()
        .Any(e => e.State != EntityState.Deleted && e.Entity.Id == id && e.Entity.ExecutionId == executionId);
}
