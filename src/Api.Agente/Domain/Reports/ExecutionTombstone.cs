namespace Api.Agente.Domain.Reports;

public sealed class ExecutionTombstone
{
    private ExecutionTombstone() { }

    public ExecutionTombstone(Guid executionId, DateTimeOffset expiredAt, DateTimeOffset purgeAt)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("ExecutionId invalido.");
        if (purgeAt <= expiredAt) throw new ArgumentException("PurgeAt deve ser posterior a ExpiredAt.");

        ExecutionId = executionId;
        ExpiredAt = expiredAt.ToUniversalTime();
        PurgeAt = purgeAt.ToUniversalTime();
    }

    public Guid ExecutionId { get; private set; }
    public DateTimeOffset ExpiredAt { get; private set; }
    public DateTimeOffset PurgeAt { get; private set; }
}
