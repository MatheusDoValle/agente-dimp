using System.Text.RegularExpressions;
using Pgvector;

namespace Api.RAG.Domain.Knowledge;

public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Identification { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<DocumentRevision> Revisions { get; set; } = new List<DocumentRevision>();
}

public sealed class DocumentRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }
    public Uri SourceUrl { get; set; } = new("https://www.confaz.fazenda.gov.br");
    public string RevisionSha256 { get; set; } = string.Empty;
    public string LayoutVersion { get; set; } = "10";
    public DateTimeOffset RetrievedAt { get; set; } = DateTimeOffset.UtcNow;
    public long ByteSize { get; set; }
    public long PageCount { get; set; }
    public string SourceStatus { get; set; } = "oficial_publicado";
    public string? StoragePath { get; set; }
    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();

    public void Validate()
    {
        if (DocumentId == Guid.Empty) throw new InvalidOperationException("DocumentId e obrigatorio.");
        if (string.IsNullOrWhiteSpace(RevisionSha256) || !Regex.IsMatch(RevisionSha256, "^[a-fA-F0-9]{64}$"))
            throw new InvalidOperationException("RevisionSha256 deve ser hash hexadecimal SHA-256 valido com 64 caracteres.");
        if (LayoutVersion != "10") throw new InvalidOperationException("LayoutVersion suportada e '10'.");
        if (ByteSize <= 0) throw new InvalidOperationException("ByteSize deve ser positivo.");
        if (PageCount <= 0) throw new InvalidOperationException("PageCount deve ser positivo.");
    }
}

public sealed class EmbeddingProfile
{
    public string ProfileId { get; set; } = string.Empty;
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "text-embedding-3-small";
    public int Dimension { get; set; } = 1536;
    public string Metric { get; set; } = "cosine";
    public string Version { get; set; } = "1.0";
    public bool Normalized { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProfileId)) throw new InvalidOperationException("ProfileId e obrigatorio.");
        if (Dimension <= 0) throw new InvalidOperationException("Dimension deve ser positiva.");
    }
}

public sealed class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RevisionId { get; set; }
    public DocumentRevision? Revision { get; set; }
    public string Text { get; set; } = string.Empty;
    public int[] Pages { get; set; } = [];
    public string Section { get; set; } = string.Empty;
    public string? RecordCode { get; set; }
    public string? FieldCode { get; set; }
    public string ChunkHash { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public ICollection<ChunkVector> Vectors { get; set; } = new List<ChunkVector>();

    public void Validate()
    {
        if (RevisionId == Guid.Empty) throw new InvalidOperationException("RevisionId e obrigatorio.");
        if (string.IsNullOrWhiteSpace(Text)) throw new InvalidOperationException("Texto do chunk nao pode ser vazio.");
        if (string.IsNullOrWhiteSpace(ChunkHash)) throw new InvalidOperationException("ChunkHash e obrigatorio.");
    }
}

public sealed class ChunkVector
{
    public Guid ChunkId { get; set; }
    public KnowledgeChunk? Chunk { get; set; }
    public string EmbeddingProfileId { get; set; } = string.Empty;
    public EmbeddingProfile? Profile { get; set; }
    public Vector Vector { get; set; } = null!;
}

public sealed class IngestionJob
{
    public Guid IngestionId { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string RevisionSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "recebida";
    public string Stage { get; set; } = "recebimento";
    public string ExtractionProfileId { get; set; } = string.Empty;
    public string ChunkProfileId { get; set; } = string.Empty;
    public string EmbeddingProfileId { get; set; } = string.Empty;
    public string[] LimitationCodes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (IngestionId == Guid.Empty) throw new InvalidOperationException("IngestionId e obrigatorio.");
        if (DocumentId == Guid.Empty) throw new InvalidOperationException("DocumentId e obrigatorio.");
        if (string.IsNullOrWhiteSpace(RevisionSha256) || !Regex.IsMatch(RevisionSha256, "^[a-fA-F0-9]{64}$"))
            throw new InvalidOperationException("RevisionSha256 deve ser hash hexadecimal SHA-256 valido.");
        if (string.IsNullOrWhiteSpace(ExtractionProfileId)) throw new InvalidOperationException("ExtractionProfileId e obrigatorio.");
        if (string.IsNullOrWhiteSpace(ChunkProfileId)) throw new InvalidOperationException("ChunkProfileId e obrigatorio.");
        if (string.IsNullOrWhiteSpace(EmbeddingProfileId)) throw new InvalidOperationException("EmbeddingProfileId e obrigatorio.");
    }
}

public sealed class IngestionWorkItem
{
    public Guid IngestionJobId { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? HeartbeatAt { get; set; }
    public long FencingToken { get; set; }
    public int CheckpointVersion { get; set; }
}

public sealed class IngestionChunkCheckpoint
{
    public Guid IngestionJobId { get; set; }
    public string ChunkHash { get; set; } = string.Empty;
    public string EmbeddingProfileId { get; set; } = string.Empty;
    public Guid? CallAttemptId { get; set; }
    public string State { get; set; } = "pending";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RetrievalEvaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmbeddingProfileId { get; set; } = string.Empty;
    public string ReferenceSetId { get; set; } = string.Empty;
    public double RecallAtK { get; set; }
    public double Mrr { get; set; }
    public bool PassDecision { get; set; }
    public DateTimeOffset MeasuredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}
