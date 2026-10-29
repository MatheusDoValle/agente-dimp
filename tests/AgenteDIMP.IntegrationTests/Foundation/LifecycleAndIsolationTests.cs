using System.Text;
using Api.Agente.Application.Lifecycle;
using Api.Agente.Application.Privacy;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Foundation;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public sealed class LifecycleAndIsolationTests(FoundationDatabaseFixture fixture)
{
    private static Execution NewExecution(DateTimeOffset now) => new(Guid.NewGuid(), Guid.NewGuid(), "test-operator", "test-context", now);
    private static IExecutionMemoryCleanup NoMemory() => new DisabledExecutionMemoryCleanup(new ConfigurationBuilder().Build());

    [Theory]
    [InlineData(ExecutionState.Completed)]
    [InlineData(ExecutionState.PartiallyCompleted)]
    [InlineData(ExecutionState.Failed)]
    [InlineData(ExecutionState.Cancelled)]
    public async Task TerminalStateIsPublishedOnlyAfterOriginalIndexMapAndMemoryAreClean(ExecutionState terminal)
    {
        using var area = new TestArtifactArea();
        var clock = new ControlledClock(DateTimeOffset.Parse("2026-10-29T12:00:00Z"));
        var sanitizer = area.CreateSanitizer(clock);
        var artifacts = new TemporaryArtifactStore(new TemporaryStorageOptions { RootPath = area.Root }, clock);
        var execution = NewExecution(clock.Now);
        var summary = await sanitizer.SanitizeAsync(execution.Id, "CPF 00000000000");
        await using (var source = new MemoryStream(Encoding.UTF8.GetBytes("original sintetico 00000000000")))
            await artifacts.CreateAsync(execution.Id, TemporaryArtifactKind.Original, source);
        TemporaryArtifact index;
        await using (var source = new MemoryStream([1, 2, 3]))
            index = await artifacts.CreateAsync(execution.Id, TemporaryArtifactKind.ParserIndex, source);
        var directory = Path.Combine(area.Root, execution.Id.ToString("N"));
        var indexPath = Path.Combine(directory, index.ArtifactId.ToString("N") + ".sqlite");
        await File.WriteAllTextAsync(indexPath + "-wal", "sintetico");
        await File.WriteAllTextAsync(indexPath + "-shm", "sintetico");
        await using var database = fixture.CreateAgent(sanitizer);
        database.Executions.Add(execution);
        if (terminal != ExecutionState.Failed) database.Reports.Add(new Report(execution.Id, summary, clock.Now));
        await database.SaveChangesAsync();
        var memory = new MemoryProbe();
        clock.Now = clock.Now.AddSeconds(10);
        var result = await new ExecutionFinalizer(database, artifacts, memory, clock).FinalizeAsync(execution.Id, terminal);
        Assert.True(result.Ended);
        Assert.True(memory.Called);
        Assert.False(Directory.Exists(directory));
        Assert.Empty(await artifacts.ListAsync(execution.Id));
        Assert.Equal(clock.Now, execution.EndedAt);
        Assert.Equal(terminal, execution.Status);
        Assert.Equal(ExecutionVerdict.Inconclusive, execution.Verdict);
        var report = await database.Reports.SingleOrDefaultAsync(x => x.ExecutionId == execution.Id);
        if (report is not null) Assert.Equal(clock.Now.AddDays(30), report.ExpiresAt);
        Assert.Equal(report?.ExpiresAt, execution.ReportExpiresAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sanitizer.SanitizeAsync(execution.Id, "nao reabrir"));
    }

    [Fact]
    public async Task OpenReaderPreventsFalseCleanupAndRetrySucceedsAfterDisposal()
    {
        using var area = new TestArtifactArea();
        var clock = TimeProvider.System;
        var execution = NewExecution(clock.GetUtcNow());
        var sanitizer = area.CreateSanitizer();
        var artifacts = new TemporaryArtifactStore(new TemporaryStorageOptions { RootPath = area.Root }, clock);
        var summary = await sanitizer.SanitizeAsync(execution.Id, "resultado sintetico");
        TemporaryArtifact original;
        await using (var source = new MemoryStream([1, 2, 3]))
            original = await artifacts.CreateAsync(execution.Id, TemporaryArtifactKind.Original, source);
        await using var database = fixture.CreateAgent(sanitizer);
        database.Executions.Add(execution);
        database.Reports.Add(new Report(execution.Id, summary, clock.GetUtcNow()));
        await database.SaveChangesAsync();
        var finalizer = new ExecutionFinalizer(database, artifacts, NoMemory(), clock);
        var reader = await artifacts.OpenReadAsync(execution.Id, original.ArtifactId);
        try
        {
            var pending = await finalizer.FinalizeAsync(execution.Id, ExecutionState.Completed);
            Assert.False(pending.Ended);
            Assert.Null(execution.EndedAt);
            Assert.Equal(ExecutionStage.Cleanup, execution.Stage);
            Assert.Equal(ExecutionState.Processing, execution.Status);
            Assert.Equal("CLEANUP_UNCONFIRMED", execution.TechnicalFailureCode);
            await using var verifier = fixture.CreateAgent();
            var persisted = await verifier.Executions.SingleAsync(x => x.Id == execution.Id);
            Assert.Null(persisted.EndedAt);
            Assert.Equal(ExecutionStage.Cleanup, persisted.Stage);
        }
        finally { await reader.DisposeAsync(); }
        Assert.True((await finalizer.FinalizeAsync(execution.Id, ExecutionState.Completed)).Ended);
    }

    [Fact]
    public async Task UnconfirmedMemoryDeletionKeepsExecutionInCleanup()
    {
        using var area = new TestArtifactArea();
        var execution = NewExecution(DateTimeOffset.UtcNow);
        var sanitizer = area.CreateSanitizer();
        var summary = await sanitizer.SanitizeAsync(execution.Id, "resultado sintetico");
        await using var database = fixture.CreateAgent(sanitizer);
        database.Executions.Add(execution);
        database.Reports.Add(new Report(execution.Id, summary, DateTimeOffset.UtcNow));
        await database.SaveChangesAsync();
        var artifacts = new TemporaryArtifactStore(new TemporaryStorageOptions { RootPath = area.Root }, TimeProvider.System);
        var pending = await new ExecutionFinalizer(database, artifacts, new MemoryProbe { Confirmed = false }, TimeProvider.System)
            .FinalizeAsync(execution.Id, ExecutionState.Completed);
        Assert.False(pending.Ended);
        Assert.Null(execution.EndedAt);
        Assert.Equal(ExecutionStage.Cleanup, execution.Stage);
        Assert.Null((await database.Reports.SingleAsync(x => x.ExecutionId == execution.Id)).ExpiresAt);
        Assert.True((await new ExecutionFinalizer(database, artifacts, NoMemory(), TimeProvider.System)
            .FinalizeAsync(execution.Id, ExecutionState.Completed)).Ended);
    }

    [Fact]
    public async Task RawPayloadIsRejectedEvenWhenEfCurrentValuesBypassDomainConstructor()
    {
        using var area = new TestArtifactArea();
        var execution = NewExecution(DateTimeOffset.UtcNow);
        var sanitizer = area.CreateSanitizer();
        var safe = await sanitizer.SanitizeAsync(execution.Id, "Pessoa Sintetica Alfa",
            [new SensitiveValue(SensitiveValueKind.Name, "Pessoa Sintetica Alfa")]);
        await using var database = fixture.CreateAgent(sanitizer);
        var report = new Report(execution.Id, safe, DateTimeOffset.UtcNow);
        database.Executions.Add(execution); database.Reports.Add(report);
        await database.SaveChangesAsync();
        database.Entry(report).Property(x => x.Summary).CurrentValue = "Pessoa Sintetica Alfa CPF 00000000000";
        await Assert.ThrowsAsync<SensitiveContentException>(() => database.SaveChangesAsync());
        database.ChangeTracker.Clear();
        Assert.Equal(safe.Value, (await database.Reports.SingleAsync(x => x.ExecutionId == execution.Id)).Summary);
    }

    [Fact]
    public async Task FindingCannotReferenceEvidenceFromAnotherExecution()
    {
        using var area = new TestArtifactArea();
        var first = NewExecution(DateTimeOffset.UtcNow);
        var second = NewExecution(DateTimeOffset.UtcNow);
        var sanitizer = area.CreateSanitizer();
        var firstSafe = await sanitizer.SanitizeAsync(first.Id, "evidencia sintetica");
        var secondSafe = await sanitizer.SanitizeAsync(second.Id, "achado sintetico");
        var evidence = new EvidenceSnapshot(first.Id, EvidenceSource.Execution, firstSafe);
        await using var database = fixture.CreateAgent(sanitizer);
        database.Executions.AddRange(first, second); database.Evidence.Add(evidence);
        await database.SaveChangesAsync();
        database.Findings.Add(new Finding(second.Id, "DIMP10.TEST", FindingCategory.StructuralError,
            FindingSeverity.Error, FindingOrigin.Deterministic, secondSafe,
            new DocumentLocation(1, 0, "0000", 1, null), [evidence.Id]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseAllowsOnlyOneReportPerExecution()
    {
        using var area = new TestArtifactArea();
        var execution = NewExecution(DateTimeOffset.UtcNow);
        var sanitizer = area.CreateSanitizer();
        var safe = await sanitizer.SanitizeAsync(execution.Id, "resultado sintetico");
        await using (var database = fixture.CreateAgent(sanitizer))
        {
            database.Executions.Add(execution); database.Reports.Add(new Report(execution.Id, safe, DateTimeOffset.UtcNow));
            await database.SaveChangesAsync();
        }
        await using var duplicate = fixture.CreateAgent(sanitizer);
        duplicate.Reports.Add(new Report(execution.Id, safe, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    private sealed class MemoryProbe : IExecutionMemoryCleanup
    {
        public bool Called { get; private set; }
        public bool Confirmed { get; init; } = true;
        public Task<bool> DeleteAndConfirmAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Task.FromResult(Confirmed);
        }
    }
}
