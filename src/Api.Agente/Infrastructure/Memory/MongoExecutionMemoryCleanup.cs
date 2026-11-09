using Api.Agente.Application.Lifecycle;

namespace Api.Agente.Infrastructure.Memory;

public sealed class MongoExecutionMemoryCleanup(
    IMongoConversationContextStore? mongoStore = null) : IExecutionMemoryCleanup
{
    public async Task<bool> DeleteAndConfirmAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        if (mongoStore is null) return true;

        await mongoStore.DeleteByExecutionIdAsync(executionId, cancellationToken);
        var remaining = await mongoStore.GetActiveContextAsync(executionId, cancellationToken);
        return remaining is null;
    }
}
