using System.Net;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US1;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class DurableJobRecoveryTests(FoundationDatabaseFixture fixture)
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
    public async Task ConcurrentWorkersReceiveDistinctJobs()
    {
        var store = CreateStore();
        await using var db = fixture.CreateAgent();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");

        var exec1 = new Execution(Guid.NewGuid(), Guid.NewGuid(), "worker-test", "ctx", DateTimeOffset.UtcNow);
        var exec2 = new Execution(Guid.NewGuid(), Guid.NewGuid(), "worker-test", "ctx", DateTimeOffset.UtcNow);
        db.Executions.AddRange(exec1, exec2);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(exec1.Id, now);
        await store.EnqueueAsync(exec2.Id, now);

        var task1 = store.TryLeaseNextAsync("worker-A", TimeSpan.FromMinutes(2), now);
        var task2 = store.TryLeaseNextAsync("worker-B", TimeSpan.FromMinutes(2), now);

        var leases = await Task.WhenAll(task1, task2);
        var lease1 = leases[0];
        var lease2 = leases[1];

        Assert.NotNull(lease1);
        Assert.NotNull(lease2);
        Assert.NotEqual(lease1.ExecutionId, lease2.ExecutionId);
        Assert.NotEqual(lease1.WorkerId, lease2.WorkerId);

        // Third worker gets nothing
        var lease3 = await store.TryLeaseNextAsync("worker-C", TimeSpan.FromMinutes(2), now);
        Assert.Null(lease3);

        // Cleanup
        await store.CompleteJobAsync(lease1.ExecutionId, lease1.WorkerId, lease1.FencingToken, DateTimeOffset.UtcNow);
        await store.CompleteJobAsync(lease2.ExecutionId, lease2.WorkerId, lease2.FencingToken, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExpiredLeaseCanBeReclaimedByAnotherWorker()
    {
        var store = CreateStore();
        await using var db = fixture.CreateAgent();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");

        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "worker-test", "ctx", DateTimeOffset.UtcNow);
        db.Executions.Add(exec);
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        await store.EnqueueAsync(exec.Id, now);

        // Worker A leases with very short duration
        var leaseA = await store.TryLeaseNextAsync("worker-A", TimeSpan.FromMilliseconds(50), now);
        Assert.NotNull(leaseA);
        Assert.True(leaseA.FencingToken >= 1L);

        // Wait for lease to expire
        await Task.Delay(100);
        var future = DateTimeOffset.UtcNow.AddSeconds(5);

        // Worker B can reclaim because lease expired
        var leaseB = await store.TryLeaseNextAsync("worker-B", TimeSpan.FromMinutes(2), future);
        Assert.NotNull(leaseB);
        Assert.Equal(exec.Id, leaseB.ExecutionId);
        Assert.Equal("worker-B", leaseB.WorkerId);
        Assert.True(leaseB.FencingToken > leaseA.FencingToken); // Fencing token incremented!

        // Worker A tries heartbeat with old token: must fail!
        var heartA = await store.HeartbeatAsync(exec.Id, "worker-A", leaseA.FencingToken, TimeSpan.FromMinutes(5), future);
        Assert.False(heartA);

        // Worker B completes
        var completed = await store.CompleteJobAsync(exec.Id, "worker-B", leaseB.FencingToken, future);
        Assert.True(completed);
    }

    [Fact]
    public async Task WorkerRecoversGracefullyWhenArtifactMissing()
    {
        using var area = new TestArtifactArea();
        await using var db = fixture.CreateAgent();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");

        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        var store = CreateStore();

        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "missing-file", "ctx", DateTimeOffset.UtcNow);
        db.Executions.Add(exec);
        await db.SaveChangesAsync();

        // Enqueue job but DO NOT create original artifact
        await store.EnqueueAsync(exec.Id, DateTimeOffset.UtcNow);

        // Wait for worker to pick up and fail gracefully
        using var client = host.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ExecutionStatus state;
        do
        {
            using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}", deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            state = ContractJson.Deserialize<ExecutionStatus>(await response.Content.ReadAsStringAsync(deadline.Token));
            if (state.EndedAt is null) await Task.Delay(100, deadline.Token);
        } while (state.EndedAt is null);

        Assert.Equal(ExecutionStatusStatusValue.Falhou, state.Status);
    }
}
