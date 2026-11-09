using System.Net;
using AgenteDIMP.IntegrationTests.Foundation;
using AgenteDIMP.IntegrationTests.US1;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Lifecycle;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ReportContract = Api.Agente.Application.Contracts.Report;

namespace AgenteDIMP.IntegrationTests.US2;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class PartialResultsTests(FoundationDatabaseFixture fixture)
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
    public async Task Report_WithPreConstructedPartialResults_ReflectsIncompletenessAndRequiresHumanReview()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            db.Executions.Add(exec);

            // Pre-constructed evaluations simulating SF-001 conflict and incomplete steps
            var evals = new List<RuleEvaluation>
            {
                new(execId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
                new(execId, "DIMP10.R0100.F02", "line:2", RuleApplicability.Applicable, RuleResult.Failed),
                new(execId, "DIMP10.R0200.F03", "line:3", RuleApplicability.Undetermined, RuleResult.BlockedBySourceConflict),
                new(execId, "DIMP10.R0300.F04", "line:4", RuleApplicability.Undetermined, RuleResult.NotExecuted)
            };
            db.RuleEvaluations.AddRange(evals);

            var evText = await sanitizer.SanitizeAsync(execId, "Evidencia de inconsistencia.");
            var evidence = new EvidenceSnapshot(execId, EvidenceSource.MaskedFile, evText);
            db.Evidence.Add(evidence);

            var desc = await sanitizer.SanitizeAsync(execId, "Regra violada.");
            var finding = new Api.Agente.Domain.Validation.Finding(
                execId,
                "DIMP10.R0100.F02",
                FindingCategory.BusinessInconsistency,
                FindingSeverity.Error,
                FindingOrigin.Deterministic,
                desc,
                new DocumentLocation(2, 50, "0100", 2, null),
                [evidence.Id]);
            db.Findings.Add(finding);

            await db.SaveChangesAsync();
        }

        // Build report using BuildExecutionReport
        using (var scope = host.Services.CreateScope())
        {
            var builder = scope.ServiceProvider.GetRequiredService<BuildExecutionReport>();
            await builder.ExecuteAsync(execId);

            var finalizer = scope.ServiceProvider.GetRequiredService<ExecutionFinalizer>();
            var finResult = await finalizer.FinalizeAsync(execId, ExecutionState.PartiallyCompleted);
            Assert.True(finResult.Ended);
        }

        // Query the composed report via HTTP
        using var response = await client.GetAsync($"/api/v1/analises/{execId}/relatorio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = ContractJson.Deserialize<ReportContract>(await response.Content.ReadAsStringAsync());

        // Verifications of US2 Incompleteness policy:
        Assert.Equal(execId, report.ExecutionId);
        Assert.Equal(ReportStatusValue.ConcluidaParcialmente, report.Status);
        Assert.Equal(ReportVerdictValue.ComInconsistencias, report.Verdict);
        Assert.True(report.RequiresHumanReview);
        Assert.Contains("BLOCKED_BY_SOURCE_CONFLICT", report.LimitationCodes);
        Assert.Contains("VALIDATION_INCOMPLETE", report.LimitationCodes);

        // Verification of Coverage counts:
        Assert.Equal(4L, report.Coverage.Applicable);
        Assert.Equal(1L, report.Coverage.Passed);
        Assert.Equal(1L, report.Coverage.Failed);
        Assert.Equal(1L, report.Coverage.NotExecuted);
        Assert.Equal(1L, report.Coverage.BlockedBySourceConflict);
        Assert.Equal(report.Coverage.Applicable,
            report.Coverage.Passed + report.Coverage.Failed + report.Coverage.NotExecuted + report.Coverage.BlockedBySourceConflict);

        // Query findings page
        using var findingsResponse = await client.GetAsync($"/api/v1/analises/{execId}/achados");
        Assert.Equal(HttpStatusCode.OK, findingsResponse.StatusCode);

        var findingsPage = ContractJson.Deserialize<FindingsPage>(await findingsResponse.Content.ReadAsStringAsync());
        Assert.Equal(1L, findingsPage.Total);
        Assert.Single(findingsPage.Items);
        Assert.Equal("DIMP10.R0100.F02", findingsPage.Items[0].RuleId);
    }

    [Fact]
    public async Task Report_PreservesDeterministicFindings_WhenAiAnalysisIsMissing()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            db.Executions.Add(exec);

            var evals = new List<RuleEvaluation>
            {
                new(execId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
                new(execId, "DIMP10.R0100.F01", "line:2", RuleApplicability.Applicable, RuleResult.Failed)
            };
            db.RuleEvaluations.AddRange(evals);

            var evText = await sanitizer.SanitizeAsync(execId, "Trecho mascarado");
            var evidence = new EvidenceSnapshot(execId, EvidenceSource.MaskedFile, evText);
            db.Evidence.Add(evidence);

            var desc = await sanitizer.SanitizeAsync(execId, "Erro no campo de data");
            var finding = new Api.Agente.Domain.Validation.Finding(
                execId,
                "DIMP10.R0100.F01",
                FindingCategory.BusinessInconsistency,
                FindingSeverity.Error,
                FindingOrigin.Deterministic,
                desc,
                new DocumentLocation(2, 100, "0100", 3, null),
                [evidence.Id]);
            db.Findings.Add(finding);

            await db.SaveChangesAsync();
        }

        // Build report without AI analysis
        using (var scope = host.Services.CreateScope())
        {
            var builder = scope.ServiceProvider.GetRequiredService<BuildExecutionReport>();
            await builder.ExecuteAsync(execId, analyticContent: null);

            var finalizer = scope.ServiceProvider.GetRequiredService<ExecutionFinalizer>();
            var finResult = await finalizer.FinalizeAsync(execId, ExecutionState.Completed);
            Assert.True(finResult.Ended);
        }

        using var response = await client.GetAsync($"/api/v1/analises/{execId}/relatorio");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = ContractJson.Deserialize<ReportContract>(await response.Content.ReadAsStringAsync());
        Assert.Null(report.Analysis); // AI analysis gracefully skipped
        Assert.Equal(1L, report.FindingsCount);
        Assert.Single(report.Evidence);
        Assert.Equal(ReportContractVersionValue.Version100, report.ContractVersion);
    }

    [Fact]
    public async Task InjectedCleanupFailure_PreventsReportPublication()
    {
        await CleanDatabaseAsync(fixture);
        using var area = new TestArtifactArea();
        await using var host = AnalysisSubmissionContractTests.Host(area.Root, fixture.AgentConnection);
        using var client = host.CreateClient();

        var now = DateTimeOffset.UtcNow;
        var execId = Guid.NewGuid();
        var exec = new Execution(execId, Guid.NewGuid(), "rep-tester", "ctx", now);
        exec.Start(now);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());

        var sanitizer = area.CreateSanitizer();
        await using (var db = fixture.CreateAgent(sanitizer))
        {
            db.Executions.Add(exec);
            var summary = await sanitizer.SanitizeAsync(execId, "Validacao com falha de limpeza.");
            var report = new Api.Agente.Domain.Reports.Report(execId, summary, now);
            db.Reports.Add(report);
            await db.SaveChangesAsync();
        }

        // Simulate failed cleanup in finalizer: lock a temporary file in artifact store
        var storageOptions = host.Services.GetRequiredService<TemporaryStorageOptions>();
        var execDir = Path.Combine(storageOptions.RootPath, execId.ToString("N"));
        Directory.CreateDirectory(execDir);
        var lockFilePath = Path.Combine(execDir, "locked.bin");
        using var lockFile = new FileStream(lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        using (var scope = host.Services.CreateScope())
        {
            var finalizer = scope.ServiceProvider.GetRequiredService<ExecutionFinalizer>();
            var finResult = await finalizer.FinalizeAsync(execId, ExecutionState.Completed);

            // Finalizer must report cleanup failure:
            Assert.False(finResult.Ended);
            Assert.Equal("CLEANUP_UNCONFIRMED", finResult.LimitationCode);
        }

        // Querying report when cleanup failed must NOT return 200 OK because execution is not ended
        using var response = await client.GetAsync($"/api/v1/analises/{execId}/relatorio");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Equal("ANALYSIS_IN_PROGRESS", problem.Code);
    }
}
