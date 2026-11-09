using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US1;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class PostgresJobStoreTests(FoundationDatabaseFixture fixture)
{
    private PostgresJobStore CreateStore()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgentDb"] = fixture.AgentConnection
            })
            .Build();
        return new PostgresJobStore(config);
    }

    [Fact]
    public async Task EnqueueAndLeaseJobSuccessfully()
    {
        var store = CreateStore();
        await using var db = fixture.CreateAgent();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");

        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "tester", "ctx", DateTimeOffset.UtcNow);
        db.Executions.Add(exec);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(exec.Id, now);

        var lease = await store.TryLeaseNextAsync("worker-1", TimeSpan.FromMinutes(5), now);
        Assert.NotNull(lease);
        Assert.Equal(exec.Id, lease.ExecutionId);
        Assert.Equal("worker-1", lease.WorkerId);
        Assert.Equal(1L, lease.FencingToken);
        Assert.Equal(1, lease.AttemptNumber);

        // Heartbeat extends lease
        var extended = await store.HeartbeatAsync(lease.ExecutionId, lease.WorkerId, lease.FencingToken, TimeSpan.FromMinutes(10), DateTimeOffset.UtcNow);
        Assert.True(extended);

        // Heartbeat with wrong fencing token fails
        var wrongToken = await store.HeartbeatAsync(lease.ExecutionId, lease.WorkerId, 999L, TimeSpan.FromMinutes(10), DateTimeOffset.UtcNow);
        Assert.False(wrongToken);

        // Update checkpoint
        var checkpoint = await store.UpdateCheckpointAsync(lease.ExecutionId, lease.WorkerId, lease.FencingToken, "validation", "{}", DateTimeOffset.UtcNow);
        Assert.True(checkpoint);

        // Complete job
        var completed = await store.CompleteJobAsync(lease.ExecutionId, lease.WorkerId, lease.FencingToken, DateTimeOffset.UtcNow);
        Assert.True(completed);

        // Trying to lease again yields null
        var nextLease = await store.TryLeaseNextAsync("worker-2", TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow);
        Assert.Null(nextLease);
    }
}
