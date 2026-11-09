namespace Api.Agente.Infrastructure.Jobs;

public enum JobReservationStatus
{
    Pending,
    Leased,
    Completing,
    Completed
}

public sealed class ProcessingJob
{
    public ProcessingJob() { }

    public ProcessingJob(Guid executionId, DateTimeOffset now)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Identidade de execucao obrigatoria.");
        ExecutionId = executionId;
        Status = JobReservationStatus.Pending;
        AvailableAt = CreatedAt = UpdatedAt = now.ToUniversalTime();
        FencingToken = 0;
        AttemptNumber = 0;
        CheckpointVersion = 0;
    }

    public Guid ExecutionId { get; set; }
    public JobReservationStatus Status { get; set; } = JobReservationStatus.Pending;
    public DateTimeOffset AvailableAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? HeartbeatAt { get; set; }
    public long FencingToken { get; set; }
    public int AttemptNumber { get; set; }
    public int CheckpointVersion { get; set; }
    public string? Stage { get; set; }
    public string? CheckpointData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record StageCheckpoint(
    string Stage,
    int CheckpointVersion,
    DateTimeOffset Timestamp,
    string? Details = null);

public sealed record JobLease(
    Guid ExecutionId,
    string WorkerId,
    long FencingToken,
    int AttemptNumber,
    DateTimeOffset LeaseExpiresAt);
