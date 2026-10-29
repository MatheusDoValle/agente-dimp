using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Foundation;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class ProviderConsumptionTests(FoundationDatabaseFixture fixture)
{
    [Fact]
    public async Task AttemptExistsBeforeDelegateAndMissingUsageStaysNull()
    {
        await using var database = fixture.CreateAgent();
        var store = new ProviderCallAttemptStore(database, TimeProvider.System);
        var request = new ProviderCallRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            ProviderOperation.ChatCompletion, "Fake", "fake-analysis", 1, AttemptReason.Initial);
        Guid attemptId = default;
        var value = await store.ExecuteRecordedAsync(request, async (attempt, _) =>
        {
            attemptId = attempt.AttemptId;
            await using var verifier = fixture.CreateAgent();
            var persisted = await verifier.ProviderCallAttempts.SingleAsync(x => x.AttemptId == attemptId);
            Assert.Equal(AttemptStatus.Calling, persisted.Status);
            return new ProviderResponse<string>("synthetic", new UsageMeasurement(null, null, null, null));
        });
        Assert.Equal("synthetic", value);
        database.ChangeTracker.Clear();
        var result = await database.ProviderCallAttempts.SingleAsync(x => x.AttemptId == attemptId);
        Assert.Equal(AttemptStatus.Succeeded, result.Status);
        Assert.Equal(UsageState.Unknown, result.UsageState);
        Assert.Null(result.TokensInput);
        Assert.Null(result.EstimatedCost);
        Assert.Null(result.ReturnedModel);
    }

    [Fact]
    public async Task RagIngestionCanBeCorrelatedWithoutAnExecutionAndRecordsReportedEmbeddingUsage()
    {
        await using var database = fixture.CreateRag();
        var store = new Api.RAG.Infrastructure.Usage.ProviderCallAttemptStore(database, TimeProvider.System);
        var request = new Api.RAG.Infrastructure.Usage.ProviderCallRequest(Guid.NewGuid(), Guid.NewGuid(), null,
            Guid.NewGuid(), Api.RAG.Infrastructure.Usage.ProviderOperation.IngestionEmbedding,
            "Fake", "fake-embedding", 1, Api.RAG.Infrastructure.Usage.AttemptReason.Initial);
        var attempt = await store.BeginAsync(request);
        await store.CompleteAsync(attempt, new Api.RAG.Infrastructure.Usage.UsageMeasurement(null, null, null, 25));
        database.ChangeTracker.Clear();
        var result = await database.ProviderCallAttempts.SingleAsync(x => x.AttemptId == attempt.AttemptId);
        Assert.Null(result.ExecutionId);
        Assert.Equal(request.IngestionJobId, result.IngestionJobId);
        Assert.Equal(25L, result.EmbeddingTokens);
        Assert.Equal(Api.RAG.Infrastructure.Usage.UsageState.Reported, result.UsageState);
    }
}
