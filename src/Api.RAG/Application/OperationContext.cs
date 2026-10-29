namespace Api.RAG.Application;

public sealed record OperationContext(Guid OperationId, Guid CorrelationId, DateTimeOffset CreatedAt)
{
    public static OperationContext Create(TimeProvider clock) =>
        new(Guid.NewGuid(), Guid.NewGuid(), clock.GetUtcNow().ToUniversalTime());
}
