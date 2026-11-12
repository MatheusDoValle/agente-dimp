using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Infrastructure.Persistence;

public sealed class KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : DbContext(options)
{
    public DbSet<ProviderCallAttempt> ProviderCallAttempts => Set<ProviderCallAttempt>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentRevision> DocumentRevisions => Set<DocumentRevision>();
    public DbSet<EmbeddingProfile> EmbeddingProfiles => Set<EmbeddingProfile>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<ChunkVector> ChunkVectors => Set<ChunkVector>();
    public DbSet<IngestionJob> IngestionJobs => Set<IngestionJob>();
    public DbSet<IngestionWorkItem> IngestionWorkItems => Set<IngestionWorkItem>();
    public DbSet<IngestionChunkCheckpoint> IngestionChunkCheckpoints => Set<IngestionChunkCheckpoint>();
    public DbSet<RetrievalEvaluation> RetrievalEvaluations => Set<RetrievalEvaluation>();

    internal IQueryable<ChunkVector> PublishedVectors() =>
        from vector in ChunkVectors
        where (from checkpoint in IngestionChunkCheckpoints
               join job in IngestionJobs on checkpoint.IngestionJobId equals job.IngestionId
               where checkpoint.State == "persisted"
                   && job.Status == "concluida"
                   && job.RevisionSha256 == vector.Chunk!.Revision!.RevisionSha256
                   && checkpoint.ChunkHash == vector.Chunk.ChunkHash
                   && job.EmbeddingProfileId == vector.EmbeddingProfileId
                   && checkpoint.EmbeddingProfileId == vector.EmbeddingProfileId
               select checkpoint).Any()
        select vector;

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("rag");
        model.HasPostgresExtension("vector");

        model.Entity<ProviderCallAttempt>(b =>
        {
            b.ToTable("provider_call_attempts");
            b.HasKey(x => x.AttemptId);
            b.Property(x => x.Operation).HasConversion<string>();
            b.Property(x => x.Reason).HasConversion<string>();
            b.Property(x => x.Status).HasConversion<string>();
            b.Property(x => x.UsageState).HasConversion<string>();
            b.Property(x => x.EstimatedCost).HasPrecision(28, 12);
            b.HasIndex(x => new { x.ExecutionId, x.StartedAt });
            b.HasIndex(x => new { x.IngestionJobId, x.StartedAt });
            b.HasIndex(x => new { x.LogicalCallId, x.AttemptNumber }).IsUnique();
        });

        model.Entity<Document>(b =>
        {
            b.ToTable("documents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Identification).IsRequired().HasMaxLength(256);
        });

        model.Entity<DocumentRevision>(b =>
        {
            b.ToTable("document_revisions");
            b.HasKey(x => x.Id);
            b.Property(x => x.RevisionSha256).IsRequired().HasMaxLength(64);
            b.Property(x => x.LayoutVersion).IsRequired().HasMaxLength(16);
            b.Property(x => x.SourceStatus).IsRequired().HasMaxLength(64);
            b.Property(x => x.SourceUrl).HasConversion(u => u.ToString(), s => new Uri(s));
            b.HasIndex(x => x.RevisionSha256).IsUnique();
            b.HasOne(x => x.Document).WithMany(d => d.Revisions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<EmbeddingProfile>(b =>
        {
            b.ToTable("embedding_profiles");
            b.HasKey(x => x.ProfileId);
            b.Property(x => x.Provider).IsRequired().HasMaxLength(64);
            b.Property(x => x.Model).IsRequired().HasMaxLength(128);
            b.Property(x => x.Metric).IsRequired().HasMaxLength(32);
            b.Property(x => x.Version).IsRequired().HasMaxLength(32);
        });

        model.Entity<KnowledgeChunk>(b =>
        {
            b.ToTable("knowledge_chunks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Text).IsRequired();
            b.Property(x => x.Section).IsRequired().HasMaxLength(256);
            b.Property(x => x.RecordCode).HasMaxLength(16);
            b.Property(x => x.FieldCode).HasMaxLength(64);
            b.Property(x => x.ChunkHash).IsRequired().HasMaxLength(64);
            b.HasOne(x => x.Revision).WithMany(r => r.Chunks).HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.RevisionId, x.ChunkHash }).IsUnique();
            b.HasIndex(x => new { x.RevisionId, x.RecordCode });
        });

        model.Entity<ChunkVector>(b =>
        {
            b.ToTable("chunk_vectors");
            b.HasKey(x => new { x.ChunkId, x.EmbeddingProfileId });
            b.Property(x => x.Vector).HasColumnType("vector(1536)");
            b.HasOne(x => x.Chunk).WithMany(c => c.Vectors).HasForeignKey(x => x.ChunkId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Profile).WithMany().HasForeignKey(x => x.EmbeddingProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<IngestionJob>(b =>
        {
            b.ToTable("ingestion_jobs");
            b.HasKey(x => x.IngestionId);
            b.Property(x => x.RevisionSha256).IsRequired().HasMaxLength(64);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Stage).IsRequired().HasMaxLength(32);
            b.Property(x => x.ExtractionProfileId).IsRequired().HasMaxLength(64);
            b.Property(x => x.ChunkProfileId).IsRequired().HasMaxLength(64);
            b.Property(x => x.EmbeddingProfileId).IsRequired().HasMaxLength(64);
            b.HasIndex(x => new { x.RevisionSha256, x.ExtractionProfileId, x.ChunkProfileId, x.EmbeddingProfileId }).IsUnique();
        });

        model.Entity<IngestionWorkItem>(b =>
        {
            b.ToTable("ingestion_work_items");
            b.HasKey(x => x.IngestionJobId);
            b.Property(x => x.LeaseOwner).HasMaxLength(128);
            b.HasIndex(x => new { x.AvailableAt, x.LeaseExpiresAt });
        });

        model.Entity<IngestionChunkCheckpoint>(b =>
        {
            b.ToTable("ingestion_chunk_checkpoints");
            b.HasKey(x => new { x.IngestionJobId, x.ChunkHash, x.EmbeddingProfileId });
            b.Property(x => x.State).IsRequired().HasMaxLength(32);
        });

        model.Entity<RetrievalEvaluation>(b =>
        {
            b.ToTable("retrieval_evaluations");
            b.HasKey(x => x.Id);
            b.Property(x => x.EmbeddingProfileId).IsRequired().HasMaxLength(64);
            b.Property(x => x.ReferenceSetId).IsRequired().HasMaxLength(64);
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
        foreach (var entry in ChangeTracker.Entries<DocumentRevision>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Validate();
        foreach (var entry in ChangeTracker.Entries<EmbeddingProfile>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Validate();
        foreach (var entry in ChangeTracker.Entries<KnowledgeChunk>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Validate();
        foreach (var entry in ChangeTracker.Entries<IngestionJob>().Where(e => e.State is EntityState.Added or EntityState.Modified))
            entry.Entity.Validate();
    }
}
