// Generated from the versioned OpenAPI schemas; wire names and nullable required fields are preserved.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.RAG.Application.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Problem
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("correlationId")]
    public required Guid CorrelationId { get; init; }

    [JsonPropertyName("executionId")]
    public required Guid? ExecutionId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IngestionAccepted
{
    [JsonPropertyName("ingestionId")]
    public required Guid IngestionId { get; init; }

    [JsonPropertyName("documentId")]
    public required Guid DocumentId { get; init; }

    [JsonPropertyName("revisionSha256")]
    public required string RevisionSha256 { get; init; }

    [JsonPropertyName("statusUrl")]
    public required string StatusUrl { get; init; }

    [JsonPropertyName("status")]
    public required IngestionAcceptedStatusValue Status { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IngestionStatus
{
    [JsonPropertyName("ingestionId")]
    public required Guid IngestionId { get; init; }

    [JsonPropertyName("documentId")]
    public required Guid DocumentId { get; init; }

    [JsonPropertyName("revisionSha256")]
    public required string RevisionSha256 { get; init; }

    [JsonPropertyName("status")]
    public required IngestionStatusStatusValue Status { get; init; }

    [JsonPropertyName("stage")]
    public required IngestionStatusStageValue Stage { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("extractionProfileId")]
    public required string ExtractionProfileId { get; init; }

    [JsonPropertyName("chunkProfileId")]
    public required string ChunkProfileId { get; init; }

    [JsonPropertyName("embeddingProfileId")]
    public required string EmbeddingProfileId { get; init; }

    [JsonPropertyName("limitationCodes")]
    public required IReadOnlyList<string> LimitationCodes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Revision
{
    [JsonPropertyName("documentId")]
    public required Guid DocumentId { get; init; }

    [JsonPropertyName("revisionSha256")]
    public required string RevisionSha256 { get; init; }

    [JsonPropertyName("layoutVersion")]
    public required RevisionLayoutVersionValue LayoutVersion { get; init; }

    [JsonPropertyName("sourceUrl")]
    public required Uri SourceUrl { get; init; }

    [JsonPropertyName("pageCount")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long PageCount { get; init; }

    [JsonPropertyName("retrievedAt")]
    public required DateTimeOffset RetrievedAt { get; init; }

    [JsonPropertyName("embeddingProfileId")]
    public required string EmbeddingProfileId { get; init; }

    [JsonPropertyName("extractionProfileId")]
    public required string ExtractionProfileId { get; init; }

    [JsonPropertyName("chunkProfileId")]
    public required string ChunkProfileId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Revisions
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<Revision> Items { get; init; }
}

public enum IngestionAcceptedStatusValue
{
    [JsonStringEnumMemberName("recebida")]
    Recebida,
    [JsonStringEnumMemberName("concluida")]
    Concluida
}

public enum IngestionStatusStatusValue
{
    [JsonStringEnumMemberName("recebida")]
    Recebida,
    [JsonStringEnumMemberName("em_processamento")]
    EmProcessamento,
    [JsonStringEnumMemberName("concluida")]
    Concluida,
    [JsonStringEnumMemberName("falhou")]
    Falhou
}

public enum IngestionStatusStageValue
{
    [JsonStringEnumMemberName("recebimento")]
    Recebimento,
    [JsonStringEnumMemberName("extracao")]
    Extracao,
    [JsonStringEnumMemberName("segmentacao")]
    Segmentacao,
    [JsonStringEnumMemberName("embeddings")]
    Embeddings,
    [JsonStringEnumMemberName("publicacao")]
    Publicacao
}

public enum RevisionLayoutVersionValue
{
    [JsonStringEnumMemberName("10")]
    Version10
}
