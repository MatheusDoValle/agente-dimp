using System.Net;
using AgenteDIMP.IntegrationTests.Foundation;
using AgenteDIMP.IntegrationTests.US1;
using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Validation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US2;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class ReportContractTests(FoundationDatabaseFixture fixture)
{
    private static async Task CleanDatabaseAsync(FoundationDatabaseFixture fix)
    {
        await using var db = fix.CreateAgent();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.reports;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.findings;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.evidence;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.rule_evaluations;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.processing_jobs;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM agente.executions;");
    }

    [Fact]
    public async Task GetReport_Returns200_WhenReportExists()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Completed, now, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Validacao deterministica concluida sem inconsistencias.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, now);

            db.Executions.Add(exec);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/relatorio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reportDto = ContractJson.Deserialize<Report>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, reportDto.ExecutionId);
        Assert.Equal(ReportStatusValue.Concluida, reportDto.Status);
        Assert.Equal(ReportVerdictValue.SemInconsistenciasNasRegrasAvaliadas, reportDto.Verdict);
        Assert.Equal("1.0.0", reportDto.ContractVersion.ToString().Replace("Version", "").Insert(1, ".").Insert(3, "."));
    }

    [Fact]
    public async Task GetReport_Returns404_WhenExecutionDoesNotExist()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var nonExistentId = Guid.NewGuid();
        using var response = await client.GetAsync($"/api/v1/analises/{nonExistentId}/relatorio");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(nonExistentId, problem.ExecutionId);
        Assert.Equal("EXECUTION_NOT_FOUND", problem.Code);
    }

    [Fact]
    public async Task GetReport_Returns409_WhenAnalysisStillInProgress()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);

        await using (var db = fixture.CreateAgent())
        {
            db.Executions.Add(exec);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/relatorio");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, problem.ExecutionId);
        Assert.Equal("ANALYSIS_IN_PROGRESS", problem.Code);
    }

    [Fact]
    public async Task GetReport_Returns410_WhenReportIsExpired()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow.AddDays(-40);
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Completed, now, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Validacao antiga expirada.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, now);

            db.Executions.Add(exec);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/relatorio");
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, problem.ExecutionId);
        Assert.Equal("REPORT_EXPIRED", problem.Code);
    }

    [Fact]
    public async Task GetReport_Returns422_WhenExecutionFailedWithoutReport()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Failed, now, hasReport: false);

        await using (var db = fixture.CreateAgent())
        {
            db.Executions.Add(exec);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/relatorio");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, problem.ExecutionId);
        Assert.Equal("REPORT_UNAVAILABLE", problem.Code);
    }

    [Fact]
    public async Task GetFindings_Returns200_WithPaginationCursorAndTotal()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Completed, now, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Validacao com achados.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, now);
            db.Executions.Add(exec);
            db.Reports.Add(report);

            for (int i = 1; i <= 5; i++)
            {
                var evText = await sanitizer.SanitizeAsync(exec.Id, $"Evidencia {i}");
                var evidence = new Api.Agente.Domain.Reports.EvidenceSnapshot(exec.Id, Api.Agente.Domain.Reports.EvidenceSource.MaskedFile, evText);
                db.Evidence.Add(evidence);

                var desc = await sanitizer.SanitizeAsync(exec.Id, $"Inconsistencia {i}");
                var finding = new Api.Agente.Domain.Validation.Finding(
                    exec.Id,
                    "DIMP10.R0000.F01",
                    FindingCategory.BusinessInconsistency,
                    FindingSeverity.Error,
                    FindingOrigin.Deterministic,
                    desc,
                    new DocumentLocation(i, (i - 1) * 50, "0000", 1, null),
                    [evidence.Id]);
                db.Findings.Add(finding);
            }
            await db.SaveChangesAsync();
        }

        // Page 1: pageSize = 2
        using var page1Response = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);

        var page1 = ContractJson.Deserialize<FindingsPage>(await page1Response.Content.ReadAsStringAsync());
        Assert.Equal(5L, page1.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        // Page 2: with cursor
        using var page2Response = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados?pageSize=2&cursor={page1.NextCursor}");
        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);

        var page2 = ContractJson.Deserialize<FindingsPage>(await page2Response.Content.ReadAsStringAsync());
        Assert.Equal(5L, page2.Total);
        Assert.Equal(2, page2.Items.Count);
        Assert.NotNull(page2.NextCursor);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);

        // Page 3: final page
        using var page3Response = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados?pageSize=2&cursor={page2.NextCursor}");
        Assert.Equal(HttpStatusCode.OK, page3Response.StatusCode);

        var page3 = ContractJson.Deserialize<FindingsPage>(await page3Response.Content.ReadAsStringAsync());
        Assert.Equal(5L, page3.Total);
        Assert.Single(page3.Items);
        Assert.Null(page3.NextCursor);
    }

    [Fact]
    public async Task GetFindings_Returns400_WhenPageSizeIsOutOfBounds()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var execId = Guid.NewGuid();

        // pageSize = 0 (min is 1)
        using var zeroResponse = await client.GetAsync($"/api/v1/analises/{execId}/achados?pageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, zeroResponse.StatusCode);

        // pageSize = 501 (max is 500)
        using var tooLargeResponse = await client.GetAsync($"/api/v1/analises/{execId}/achados?pageSize=501");
        Assert.Equal(HttpStatusCode.BadRequest, tooLargeResponse.StatusCode);
    }

    [Fact]
    public async Task GetFindings_Returns400_WhenCursorIsCorruptOrFromDifferentExecution()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Completed, now, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Validacao com achados.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, now);
            db.Executions.Add(exec);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // Corrupted base64 cursor
        using var corruptResponse = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados?cursor=not-a-valid-cursor");
        Assert.Equal(HttpStatusCode.BadRequest, corruptResponse.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await corruptResponse.Content.ReadAsStringAsync());
        Assert.Equal("INVALID_CURSOR", problem.Code);
    }

    [Fact]
    public async Task GetFindings_Returns404_WhenExecutionDoesNotExist()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var nonExistentId = Guid.NewGuid();
        using var response = await client.GetAsync($"/api/v1/analises/{nonExistentId}/achados");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(nonExistentId, problem.ExecutionId);
        Assert.Equal("EXECUTION_NOT_FOUND", problem.Code);
    }

    [Fact]
    public async Task GetFindings_Returns409_WhenAnalysisStillInProgress()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);

        await using (var db = fixture.CreateAgent())
        {
            db.Executions.Add(exec);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, problem.ExecutionId);
        Assert.Equal("ANALYSIS_IN_PROGRESS", problem.Code);
    }

    [Fact]
    public async Task GetFindings_Returns410_WhenReportIsExpired()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow.AddDays(-40);
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Completed, now, hasReport: true);

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            var summary = await sanitizer.SanitizeAsync(exec.Id, "Validacao antiga expirada.");
            var report = new Api.Agente.Domain.Reports.Report(exec.Id, summary, now);

            db.Executions.Add(exec);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados");
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, problem.ExecutionId);
        Assert.Equal("REPORT_EXPIRED", problem.Code);
    }

    [Fact]
    public async Task GetFindings_Returns422_WhenExecutionFailedWithoutReport()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var exec = new Execution(Guid.NewGuid(), Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.BeginCleanup(now);
        exec.CompleteAfterCleanup(ExecutionState.Failed, now, hasReport: false);

        await using (var db = fixture.CreateAgent())
        {
            db.Executions.Add(exec);
            await db.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/v1/analises/{exec.Id}/achados");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal(exec.Id, problem.ExecutionId);
        Assert.Equal("REPORT_UNAVAILABLE", problem.Code);
    }
}
