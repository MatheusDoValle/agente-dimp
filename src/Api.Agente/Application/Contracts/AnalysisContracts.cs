// Generated from the versioned OpenAPI schemas; wire names and nullable required fields are preserved.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Agente.Application.Contracts;

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
public sealed record Accepted
{
    [JsonPropertyName("executionId")]
    public required Guid ExecutionId { get; init; }

    [JsonPropertyName("status")]
    public required AcceptedStatusValue Status { get; init; }

    [JsonPropertyName("statusUrl")]
    public required string StatusUrl { get; init; }

    [JsonPropertyName("reportUrl")]
    public required string ReportUrl { get; init; }

    [JsonPropertyName("acceptedAt")]
    public required DateTimeOffset AcceptedAt { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Coverage
{
    [JsonPropertyName("catalogVersion")]
    public required string CatalogVersion { get; init; }

    [JsonPropertyName("applicable")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long Applicable { get; init; }

    [JsonPropertyName("passed")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long Passed { get; init; }

    [JsonPropertyName("failed")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long Failed { get; init; }

    [JsonPropertyName("notExecuted")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long NotExecuted { get; init; }

    [JsonPropertyName("blockedBySourceConflict")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long BlockedBySourceConflict { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExecutionStatus
{
    [JsonPropertyName("executionId")]
    public required Guid ExecutionId { get; init; }

    [JsonPropertyName("status")]
    public required ExecutionStatusStatusValue Status { get; init; }

    [JsonPropertyName("stage")]
    public required ExecutionStatusStageValue Stage { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public required DateTimeOffset? EndedAt { get; init; }

    [JsonPropertyName("reportAvailable")]
    public required bool ReportAvailable { get; init; }

    [JsonPropertyName("reportUrl")]
    public required string? ReportUrl { get; init; }

    [JsonPropertyName("reportExpiresAt")]
    public required DateTimeOffset? ReportExpiresAt { get; init; }

    [JsonPropertyName("limitationCodes")]
    public required IReadOnlyList<string> LimitationCodes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Location
{
    [JsonPropertyName("line")]
    [Range(typeof(long), "1", "9223372036854775807")]
    public required long? Line { get; init; }

    [JsonPropertyName("byteOffset")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long? ByteOffset { get; init; }

    [JsonPropertyName("recordCode")]
    public required string? RecordCode { get; init; }

    [JsonPropertyName("fieldPosition")]
    [Range(typeof(long), "1", "9223372036854775807")]
    public required long? FieldPosition { get; init; }

    [JsonPropertyName("unavailableReason")]
    public required string? UnavailableReason { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Evidence
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("sourceKind")]
    public required EvidenceSourceKindValue SourceKind { get; init; }

    [JsonPropertyName("revisionSha256")]
    public required string? RevisionSha256 { get; init; }

    [JsonPropertyName("chunkId")]
    public required string? ChunkId { get; init; }

    [PositivePages]
    [JsonPropertyName("pages")]
    public required IReadOnlyList<long> Pages { get; init; }

    [JsonPropertyName("locator")]
    public required string? Locator { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Finding
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("ruleId")]
    public required string? RuleId { get; init; }

    [JsonPropertyName("category")]
    public required FindingCategoryValue Category { get; init; }

    [JsonPropertyName("severity")]
    public required FindingSeverityValue Severity { get; init; }

    [JsonPropertyName("origin")]
    public required FindingOriginValue Origin { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("location")]
    public required Location Location { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FindingsPage
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<Finding> Items { get; init; }

    [JsonPropertyName("nextCursor")]
    public required string? NextCursor { get; init; }

    [JsonPropertyName("total")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long Total { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UsageSummary
{
    [JsonPropertyName("inputTokens")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long? InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long? OutputTokens { get; init; }

    [JsonPropertyName("embeddingTokens")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long? EmbeddingTokens { get; init; }

    [JsonPropertyName("hasUnknownUsage")]
    public required bool HasUnknownUsage { get; init; }

    [JsonPropertyName("estimatedCost")]
    [RegularExpression(@"^[0-9]+(\.[0-9]+)?$")]
    public required string? EstimatedCost { get; init; }

    [JsonPropertyName("currency")]
    public required string? Currency { get; init; }

    [JsonPropertyName("priceBookVersionId")]
    public required string? PriceBookVersionId { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Report
{
    [JsonPropertyName("contractVersion")]
    public required ReportContractVersionValue ContractVersion { get; init; }

    [JsonPropertyName("executionId")]
    public required Guid ExecutionId { get; init; }

    [JsonPropertyName("status")]
    public required ReportStatusValue Status { get; init; }

    [JsonPropertyName("verdict")]
    public required ReportVerdictValue Verdict { get; init; }

    [JsonPropertyName("coverage")]
    public required Coverage Coverage { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("analysis")]
    public required AnalyticContent? Analysis { get; init; }

    [JsonPropertyName("requiresHumanReview")]
    public required bool RequiresHumanReview { get; init; }

    [JsonPropertyName("limitationCodes")]
    public required IReadOnlyList<string> LimitationCodes { get; init; }

    [JsonPropertyName("findingsCount")]
    [Range(typeof(long), "0", "9223372036854775807")]
    public required long FindingsCount { get; init; }

    [JsonPropertyName("findingsUrl")]
    public required string FindingsUrl { get; init; }

    [JsonPropertyName("evidence")]
    public required IReadOnlyList<Evidence> Evidence { get; init; }

    [JsonPropertyName("usage")]
    public required UsageSummary Usage { get; init; }

    [JsonPropertyName("endedAt")]
    public required DateTimeOffset EndedAt { get; init; }

    [JsonPropertyName("expiresAt")]
    public required DateTimeOffset ExpiresAt { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalyticContent
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("explanations")]
    public required IReadOnlyList<AnalyticContentExplanationsItem> Explanations { get; init; }

    [JsonPropertyName("indications")]
    public required IReadOnlyList<AnalyticContentIndicationsItem> Indications { get; init; }

    [JsonPropertyName("limitations")]
    public required IReadOnlyList<string> Limitations { get; init; }

    [JsonPropertyName("requiresHumanReview")]
    public required bool RequiresHumanReview { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalyticContentExplanationsItem
{
    [JsonPropertyName("findingIds")]
    public required IReadOnlyList<string> FindingIds { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalyticContentIndicationsItem
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("evidenceIds")]
    public required IReadOnlyList<string> EvidenceIds { get; init; }
}

public enum AcceptedStatusValue
{
    [JsonStringEnumMemberName("recebida")]
    Recebida
}

public enum ExecutionStatusStatusValue
{
    [JsonStringEnumMemberName("recebida")]
    Recebida,
    [JsonStringEnumMemberName("em_processamento")]
    EmProcessamento,
    [JsonStringEnumMemberName("concluida")]
    Concluida,
    [JsonStringEnumMemberName("concluida_parcialmente")]
    ConcluidaParcialmente,
    [JsonStringEnumMemberName("falhou")]
    Falhou,
    [JsonStringEnumMemberName("cancelada")]
    Cancelada
}

public enum ExecutionStatusStageValue
{
    [JsonStringEnumMemberName("recebimento")]
    Recebimento,
    [JsonStringEnumMemberName("identificacao")]
    Identificacao,
    [JsonStringEnumMemberName("validacao")]
    Validacao,
    [JsonStringEnumMemberName("recuperacao")]
    Recuperacao,
    [JsonStringEnumMemberName("analise")]
    Analise,
    [JsonStringEnumMemberName("composicao")]
    Composicao,
    [JsonStringEnumMemberName("limpeza")]
    Limpeza
}

public enum EvidenceSourceKindValue
{
    [JsonStringEnumMemberName("manual")]
    Manual,
    [JsonStringEnumMemberName("arquivo_mascarado")]
    ArquivoMascarado,
    [JsonStringEnumMemberName("execucao")]
    Execucao
}

public enum FindingCategoryValue
{
    [JsonStringEnumMemberName("erro_estrutural")]
    ErroEstrutural,
    [JsonStringEnumMemberName("inconsistencia_negocio")]
    InconsistenciaNegocio,
    [JsonStringEnumMemberName("indicio_ia")]
    IndicioIa,
    [JsonStringEnumMemberName("ausencia_evidencias")]
    AusenciaEvidencias,
    [JsonStringEnumMemberName("falha_tecnica")]
    FalhaTecnica
}

public enum FindingSeverityValue
{
    [JsonStringEnumMemberName("bloqueante")]
    Bloqueante,
    [JsonStringEnumMemberName("erro")]
    Erro,
    [JsonStringEnumMemberName("aviso")]
    Aviso,
    [JsonStringEnumMemberName("informativo")]
    Informativo
}

public enum FindingOriginValue
{
    [JsonStringEnumMemberName("deterministico")]
    Deterministico,
    [JsonStringEnumMemberName("ia")]
    Ia,
    [JsonStringEnumMemberName("sistema")]
    Sistema
}

public enum ReportContractVersionValue
{
    [JsonStringEnumMemberName("1.0.0")]
    Version100
}

public enum ReportStatusValue
{
    [JsonStringEnumMemberName("concluida")]
    Concluida,
    [JsonStringEnumMemberName("concluida_parcialmente")]
    ConcluidaParcialmente,
    [JsonStringEnumMemberName("falhou")]
    Falhou,
    [JsonStringEnumMemberName("cancelada")]
    Cancelada
}

public enum ReportVerdictValue
{
    [JsonStringEnumMemberName("sem_inconsistencias_nas_regras_avaliadas")]
    SemInconsistenciasNasRegrasAvaliadas,
    [JsonStringEnumMemberName("com_inconsistencias")]
    ComInconsistencias,
    [JsonStringEnumMemberName("inconclusivo")]
    Inconclusivo,
    [JsonStringEnumMemberName("nao_suportado")]
    NaoSuportado
}
