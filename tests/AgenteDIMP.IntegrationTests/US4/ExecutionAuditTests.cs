using System.Net;
using AgenteDIMP.IntegrationTests.Foundation;
using AgenteDIMP.IntegrationTests.US1;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Reports;
using Api.Agente.Application.Usage;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Infrastructure.Reports;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US4;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class ExecutionAuditTests(FoundationDatabaseFixture fixture)
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
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.price_books;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.executions;");
    }

    [Fact]
    public async Task Execution_AuditFlow_WithAttemptsAndPriceBook_PublishesAccurateUsageInReport()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var startTime = DateTimeOffset.UtcNow;
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "audit-tester", "ctx", startTime);
        exec.Start(startTime);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(startTime);
        exec.CompleteAfterCleanup(ExecutionState.Completed, startTime, hasReport: true);

        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = startTime,
            ValidFrom = startTime,
            Currency = "USD",
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    InputPricePerMillion = 2.50m,
                    OutputPricePerMillion = 10.00m
                }
            ]
        };

        var logicalCallId = Guid.NewGuid();
        var attempt1Request = new ProviderCallRequest(
            LogicalCallId: logicalCallId,
            CorrelationId: Guid.NewGuid(),
            ExecutionId: execId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt2Request = new ProviderCallRequest(
            LogicalCallId: logicalCallId,
            CorrelationId: Guid.NewGuid(),
            ExecutionId: execId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 2,
            Reason: AttemptReason.TransientRetry);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            db.Executions.Add(exec);
            db.PriceBooks.Add(priceBook);

            var store = new ProviderCallAttemptStore(db, TimeProvider.System);
            // Attempt 1: failed / transient
            var a1 = await store.BeginAsync(attempt1Request);
            a1.Fail(startTime.AddSeconds(1), cancelled: false);
            await db.SaveChangesAsync();

            // Attempt 2: retry succeeded with tokens
            var a2 = await store.BeginAsync(attempt2Request);
            await store.CompleteAsync(a2, new UsageMeasurement(4000, 1000, null, null), "gpt-4o", "req-audit-2");

            var summaryText = await sanitizer.SanitizeAsync(execId, "Relatorio de auditoria de execucao.");
            var report = new Api.Agente.Domain.Reports.Report(execId, summaryText, startTime);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // Fetch report through HTTP endpoint
        using var response = await client.GetAsync($"/api/v1/analises/{execId}/relatorio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reportDto = ContractJson.Deserialize<Api.Agente.Application.Contracts.Report>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(reportDto.Usage);
        Assert.Equal(4000, reportDto.Usage.InputTokens);
        Assert.Equal(1000, reportDto.Usage.OutputTokens);
        Assert.False(reportDto.Usage.HasUnknownUsage);

        // Cost calculation: 4000/1M * 2.50 = 0.010 + 1000/1M * 10.00 = 0.010 = 0.020
        Assert.NotNull(reportDto.Usage.EstimatedCost);
        Assert.Equal("0.02", reportDto.Usage.EstimatedCost);
        Assert.Equal("USD", reportDto.Usage.Currency);
    }

    [Fact]
    public async Task Execution_AuditFlow_WithUnknownUsageState_PropagatesHasUnknownUsageTrue()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var startTime = DateTimeOffset.UtcNow;
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "audit-unknown-tester", "ctx", startTime);
        exec.Start(startTime);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(startTime);
        exec.CompleteAfterCleanup(ExecutionState.Completed, startTime, hasReport: true);

        var attemptRequest = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: execId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            db.Executions.Add(exec);

            var store = new ProviderCallAttemptStore(db, TimeProvider.System);
            var a = await store.BeginAsync(attemptRequest);
            // Succeeded with null usage (unknown)
            await store.CompleteAsync(a, new UsageMeasurement(null, null, null, null), "gpt-4o", "req-unknown");

            var summaryText = await sanitizer.SanitizeAsync(execId, "Relatorio com consumo desconhecido.");
            var report = new Api.Agente.Domain.Reports.Report(execId, summaryText, startTime);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{execId}/relatorio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reportDto = ContractJson.Deserialize<Api.Agente.Application.Contracts.Report>(await response.Content.ReadAsStringAsync());
        Assert.NotNull(reportDto.Usage);
        Assert.True(reportDto.Usage.HasUnknownUsage);
        Assert.Null(reportDto.Usage.EstimatedCost);
    }
}
