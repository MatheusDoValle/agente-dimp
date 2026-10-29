using System.Text.RegularExpressions;

namespace Api.RAG.Infrastructure.Usage;

public enum ProviderOperation { ChatCompletion, QueryEmbedding, IngestionEmbedding }
public enum AttemptReason { Initial, TransientRetry, SemanticRepair }
public enum AttemptStatus { Calling, Succeeded, Failed, Cancelled, Unknown }
public enum UsageState { Reported, Partial, Unknown }

public sealed record ProviderCallRequest(Guid LogicalCallId, Guid CorrelationId, Guid? ExecutionId,
    Guid? IngestionJobId, ProviderOperation Operation, string Provider, string Model, int AttemptNumber, AttemptReason Reason);

public sealed record UsageMeasurement(long? InputTokens, long? OutputTokens, long? CachedTokens, long? EmbeddingTokens)
{
    public void Validate()
    {
        if (InputTokens is < 0 || OutputTokens is < 0 || CachedTokens is < 0 || EmbeddingTokens is < 0)
            throw new ArgumentException("Consumo negativo.");
        if (CachedTokens.HasValue && InputTokens.HasValue && CachedTokens > InputTokens)
            throw new ArgumentException("Tokens em cache excedem tokens de entrada.");
    }
}

public sealed record ProviderResponse<T>(T Value, UsageMeasurement Usage, string? ReturnedModel = null, string? ProviderRequestId = null);

public sealed class ProviderCallAttempt
{
    private ProviderCallAttempt() { }
    internal ProviderCallAttempt(ProviderCallRequest request, DateTimeOffset startedAt)
    {
        AttemptId = Guid.NewGuid(); LogicalCallId = request.LogicalCallId; CorrelationId = request.CorrelationId;
        ExecutionId = request.ExecutionId; IngestionJobId = request.IngestionJobId; Operation = request.Operation;
        Provider = request.Provider; RequestedModel = request.Model; AttemptNumber = request.AttemptNumber; Reason = request.Reason;
        StartedAt = startedAt.ToUniversalTime();
        Validate();
    }

    public Guid AttemptId { get; private set; }
    public Guid LogicalCallId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid? ExecutionId { get; private set; }
    public Guid? IngestionJobId { get; private set; }
    public string ServiceOwner { get; private set; } = "Api.RAG";
    public ProviderOperation Operation { get; private set; }
    public string Provider { get; private set; } = "";
    public string RequestedModel { get; private set; } = "";
    public string? ReturnedModel { get; private set; }
    public string? ProviderRequestId { get; private set; }
    public int AttemptNumber { get; private set; }
    public AttemptReason Reason { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public double? DurationMilliseconds { get; private set; }
    public AttemptStatus Status { get; private set; } = AttemptStatus.Calling;
    public long? TokensInput { get; private set; }
    public long? TokensOutput { get; private set; }
    public long? CachedTokens { get; private set; }
    public long? EmbeddingTokens { get; private set; }
    public UsageState UsageState { get; private set; } = UsageState.Unknown;
    public decimal? EstimatedCost { get; private set; }
    public string? Currency { get; private set; }
    public Guid? PriceBookVersionId { get; private set; }
    public string EstimateStatus { get; private set; } = "unknown";

    public void Validate()
    {
        if (AttemptId == Guid.Empty || LogicalCallId == Guid.Empty || CorrelationId == Guid.Empty
            || AttemptNumber is < 1 or > 4 || ServiceOwner != "Api.RAG"
            || !Enum.IsDefined(Operation) || !Enum.IsDefined(Reason))
            throw new ArgumentException("Metadados de tentativa invalidos.");
        if (ExecutionId is null && (Operation != ProviderOperation.IngestionEmbedding || IngestionJobId is null || IngestionJobId == Guid.Empty))
            throw new ArgumentException("Execucao ausente somente na ingestao de acervo identificada.");
        if (ExecutionId == Guid.Empty || IngestionJobId == Guid.Empty) throw new ArgumentException("Escopo invalido.");
        RequireTechnicalValue(Provider); RequireTechnicalValue(RequestedModel);
        if (ReturnedModel is not null) RequireTechnicalValue(ReturnedModel);
        if (ProviderRequestId is not null && !Regex.IsMatch(ProviderRequestId, "^[A-Za-z][A-Za-z0-9_-]{0,127}$"))
            throw new ArgumentException("Request ID invalido.");
        new UsageMeasurement(TokensInput, TokensOutput, CachedTokens, EmbeddingTokens).Validate();
    }

    internal void Complete(UsageMeasurement usage, DateTimeOffset endedAt, string? returnedModel, string? providerRequestId)
    {
        if (Status != AttemptStatus.Calling) throw new InvalidOperationException("Tentativa encerrada.");
        usage.Validate();
        if (returnedModel is not null) RequireTechnicalValue(returnedModel);
        if (providerRequestId is not null && !Regex.IsMatch(providerRequestId, "^[A-Za-z][A-Za-z0-9_-]{0,127}$"))
            throw new ArgumentException("Request ID invalido.");
        ReturnedModel = returnedModel; ProviderRequestId = providerRequestId;
        TokensInput = usage.InputTokens; TokensOutput = usage.OutputTokens; CachedTokens = usage.CachedTokens; EmbeddingTokens = usage.EmbeddingTokens;
        var complete = Operation == ProviderOperation.ChatCompletion
            ? TokensInput.HasValue && TokensOutput.HasValue : EmbeddingTokens.HasValue;
        UsageState = complete ? UsageState.Reported
            : usage is { InputTokens: null, OutputTokens: null, CachedTokens: null, EmbeddingTokens: null } ? UsageState.Unknown : UsageState.Partial;
        Finish(AttemptStatus.Succeeded, endedAt);
    }

    internal void Fail(DateTimeOffset endedAt, bool cancelled) =>
        Finish(cancelled ? AttemptStatus.Cancelled : AttemptStatus.Unknown, endedAt);

    private void Finish(AttemptStatus status, DateTimeOffset endedAt)
    {
        var utc = endedAt.ToUniversalTime();
        if (utc < StartedAt) throw new ArgumentException("Relogio anterior a chamada.");
        EndedAt = utc; DurationMilliseconds = (utc - StartedAt).TotalMilliseconds; Status = status;
    }

    private static void RequireTechnicalValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Za-z][A-Za-z0-9._:/-]{0,127}$")
            || value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Identidade tecnica invalida; nao fornecer segredo ou payload.");
    }
}
