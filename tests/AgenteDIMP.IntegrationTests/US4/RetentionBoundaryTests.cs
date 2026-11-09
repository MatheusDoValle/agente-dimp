using System.Net;
using AgenteDIMP.IntegrationTests.Foundation;
using AgenteDIMP.IntegrationTests.US1;
using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US4;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class RetentionBoundaryTests(FoundationDatabaseFixture fixture)
{
    private static async Task CleanDatabaseAsync(FoundationDatabaseFixture fix)
    {
        await using var db = fix.CreateAgent();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.reports;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.findings;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.evidence;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.rule_evaluations;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.execution_tombstones;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.provider_call_attempts;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.executions;");
    }

    [Fact]
    public async Task Report_IsAccessibleBefore30Days_AndReturns410ImmediatelyAt30Days()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var startTime = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new ControlledClock(startTime);

        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "retention-tester", "ctx", startTime);
        exec.Start(startTime);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(startTime);
        exec.CompleteAfterCleanup(ExecutionState.Completed, startTime, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Validacao inicial concluida.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, startTime);

            db.Executions.Add(exec);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // 1. Before 30 days (Day 29) -> report is accessible (200 OK)
        await using (var db = fixture.CreateAgent())
        {
            var store = new ReportStore(db);
            var report29 = await store.GetReportAsync(exec.Id, startTime.AddDays(29).AddHours(23));
            Assert.NotNull(report29);
            Assert.Equal(exec.Id, report29.ExecutionId);
        }

        // 2. Exactly at 30 days + 1 second -> immediately returns 410 Gone (ReportExpiredException)
        await using (var db = fixture.CreateAgent())
        {
            var store = new ReportStore(db);
            await Assert.ThrowsAsync<ReportExpiredException>(() =>
                store.GetReportAsync(exec.Id, startTime.AddDays(30).AddSeconds(1)));
        }
    }

    [Fact]
    public async Task ReportExpirationService_SweepsExpiredExecution_CreatesTombstone_AndPurgesAt90Days()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();

        var startTime = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "tombstone-tester", "ctx", startTime);
        exec.Start(startTime);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(startTime);
        exec.CompleteAfterCleanup(ExecutionState.Completed, startTime, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Relatorio para teste de tombstone.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, startTime);

            db.Executions.Add(exec);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // Run expiration sweep at Day 31
        var day31 = startTime.AddDays(31);
        await using (var db = fixture.CreateAgent())
        {
            var expirationService = new ReportExpirationService(db);
            var expiredCount = await expirationService.ExpireEligibleReportsAsync(day31);
            Assert.Equal(1, expiredCount);

            // Verify execution and report are deleted, and tombstone exists
            Assert.False(await db.Executions.AnyAsync(e => e.Id == execId));
            Assert.False(await db.Reports.AnyAsync(r => r.ExecutionId == execId));

            var tombstone = await db.ExecutionTombstones.SingleOrDefaultAsync(t => t.ExecutionId == execId);
            Assert.NotNull(tombstone);
            Assert.Equal(startTime.AddDays(30), tombstone.ExpiredAt);
            Assert.Equal(startTime.AddDays(30).AddDays(90), tombstone.PurgeAt);

            // During the 90 days post-expiration, GetReportAsync throws ReportExpiredException (410 Gone)
            var store = new ReportStore(db);
            await Assert.ThrowsAsync<ReportExpiredException>(() => store.GetReportAsync(execId, day31));
        }

        // At Day 121 (after PurgeAt = 30 + 90 = 120 days), purge tombstones
        var day121 = startTime.AddDays(121);
        await using (var db = fixture.CreateAgent())
        {
            var expirationService = new ReportExpirationService(db);
            var purgedCount = await expirationService.PurgeExpiredTombstonesAsync(day121);
            Assert.Equal(1, purgedCount);

            // Now tombstone is gone, GetReportAsync throws ExecutionNotFoundException (404 Not Found)
            var store = new ReportStore(db);
            await Assert.ThrowsAsync<ExecutionNotFoundException>(() => store.GetReportAsync(execId, day121));
        }
    }

    [Fact]
    public async Task FailedExecution_ThrowsReportUnavailable_AndDoesNotLeakSensitiveData()
    {
        await CleanDatabaseAsync(fixture);

        var startTime = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "failed-tester", "ctx", startTime);
        exec.Start(startTime);
        exec.BeginCleanup(startTime);
        exec.CompleteAfterCleanup(ExecutionState.Failed, startTime, hasReport: false);

        await using (var db = fixture.CreateAgent())
        {
            db.Executions.Add(exec);
            await db.SaveChangesAsync();

            var store = new ReportStore(db);
            await Assert.ThrowsAsync<ReportUnavailableException>(() => store.GetReportAsync(execId, startTime));
        }
    }
}
