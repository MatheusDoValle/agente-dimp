using System.Text.RegularExpressions;

namespace Api.Agente.Domain;

public enum ExecutionState { Received, Processing, Completed, PartiallyCompleted, Failed, Cancelled }
public enum ExecutionStage { Receiving, Identification, Validation, Retrieval, Analysis, Composition, Cleanup }
public enum ExecutionVerdict { Inconclusive, NoInconsistenciesInEvaluatedRules, WithInconsistencies, NotSupported }

public sealed class Execution
{
    private Execution() { }
    public Execution(Guid id, Guid correlationId, string operatorId, string contextId, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || correlationId == Guid.Empty) throw new ArgumentException("Identidade de execucao invalida.");
        RequireLocalAlias(operatorId);
        RequireLocalAlias(contextId);
        Id = id;
        CorrelationId = correlationId;
        OperatorId = operatorId;
        ContextId = contextId;
        CreatedAt = UpdatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string OperatorId { get; private set; } = "";
    public string ContextId { get; private set; } = "";
    public string IdentityAssurance { get; private set; } = "unverified_local_configuration";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ReportExpiresAt { get; private set; }
    public ExecutionState Status { get; private set; } = ExecutionState.Received;
    public ExecutionStage Stage { get; private set; } = ExecutionStage.Receiving;
    public ExecutionVerdict Verdict { get; private set; } = ExecutionVerdict.Inconclusive;
    public string? LayoutVersion { get; private set; }
    public string? DocumentRevisionSha256 { get; private set; }
    public string? RuleCatalogVersion { get; private set; }
    public Guid? ConfigurationSnapshotId { get; private set; }
    public long BytesReceived { get; private set; }
    public string? OriginalSha256 { get; private set; }
    public bool RequiresHumanReview { get; private set; }
    public string[] LimitationCodes { get; private set; } = [];
    public string? TechnicalFailureCode { get; private set; }
    public uint Version { get; private set; }

    public void Start(DateTimeOffset now)
    {
        RequireActive();
        if (Status != ExecutionState.Received) throw new InvalidOperationException("Execucao ja iniciada.");
        Status = ExecutionState.Processing;
        StartedAt = UpdatedAt = UtcAfterCreation(now);
        Stage = ExecutionStage.Identification;
    }

    public void MoveTo(ExecutionStage stage, DateTimeOffset now)
    {
        RequireActive();
        if (Status != ExecutionState.Processing || !Enum.IsDefined(stage))
            throw new InvalidOperationException("Transicao de etapa invalida.");
        if (Stage == ExecutionStage.Cleanup && stage != ExecutionStage.Cleanup)
            throw new InvalidOperationException("Limpeza nao retorna ao processamento analitico.");
        Stage = stage;
        UpdatedAt = UtcAfterCreation(now);
    }

    public void SetSource(string layoutVersion, string revisionSha256, string catalogVersion, Guid snapshotId)
    {
        RequireActive();
        if (layoutVersion != "10" || !Regex.IsMatch(revisionSha256, "^[a-fA-F0-9]{64}$") || snapshotId == Guid.Empty)
            throw new ArgumentException("Referencia documental invalida.");
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        LayoutVersion = layoutVersion;
        DocumentRevisionSha256 = revisionSha256;
        RuleCatalogVersion = catalogVersion;
        ConfigurationSnapshotId = snapshotId;
    }

    public void SetReceivedFile(long bytes, string sha256)
    {
        RequireActive();
        if (bytes < 0 || !Regex.IsMatch(sha256, "^[a-fA-F0-9]{64}$")) throw new ArgumentException("Metadados invalidos.");
        BytesReceived = bytes;
        OriginalSha256 = sha256;
    }

    internal void BeginCleanup(DateTimeOffset now)
    {
        RequireActive();
        if (Status == ExecutionState.Received) Start(now);
        MoveTo(ExecutionStage.Cleanup, now);
    }

    internal void CleanupFailed()
    {
        RequiresHumanReview = true;
        TechnicalFailureCode = "CLEANUP_UNCONFIRMED";
    }

    internal void CompleteAfterCleanup(ExecutionState terminal, DateTimeOffset now, bool hasReport)
    {
        RequireActive();
        if (!IsTerminal(terminal) || Stage != ExecutionStage.Cleanup)
            throw new InvalidOperationException("Encerramento exige limpeza.");
        Status = terminal;
        if (terminal != ExecutionState.Completed || Verdict == ExecutionVerdict.Inconclusive) RequiresHumanReview = true;
        EndedAt = UpdatedAt = UtcAfterCreation(now);
        ReportExpiresAt = hasReport ? EndedAt.Value.AddDays(30) : null;
        TechnicalFailureCode = null;
    }

    public static bool IsTerminal(ExecutionState state) =>
        state is ExecutionState.Completed or ExecutionState.PartiallyCompleted or ExecutionState.Failed or ExecutionState.Cancelled;

    private void RequireActive()
    {
        if (EndedAt.HasValue || IsTerminal(Status)) throw new InvalidOperationException("Execucao encerrada.");
    }

    private DateTimeOffset UtcAfterCreation(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        if (utc < UpdatedAt) throw new ArgumentException("Relogio anterior ao estado atual.");
        return utc;
    }

    public static void RequireLocalAlias(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")
            || Regex.IsMatch(value, "^(?:[0-9]{11}|[0-9]{14})$"))
            throw new ArgumentException("Alias tecnico local obrigatorio; nao usar CPF/CNPJ ou segredo.");
    }
}
