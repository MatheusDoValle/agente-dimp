using Api.Agente.Application.Contracts;
using Api.Agente.Application.Privacy;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Reports;
using Api.Agente.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Application.Reports;

public sealed class BuildExecutionReport(
    AgentDbContext dbContext,
    ContentSanitizer sanitizer,
    TimeProvider clock)
{
    public async Task<Contracts.Report> ExecuteAsync(
        Guid executionId,
        AnalyticContent? analyticContent = null,
        UsageSummary? usageSummary = null,
        CancellationToken cancellationToken = default)
    {
        var execution = await dbContext.Executions
            .SingleAsync(e => e.Id == executionId, cancellationToken);

        var evaluations = await dbContext.RuleEvaluations.AsNoTracking()
            .Where(r => r.ExecutionId == executionId)
            .ToListAsync(cancellationToken);

        var findings = await dbContext.Findings.AsNoTracking()
            .Where(f => f.ExecutionId == executionId)
            .ToListAsync(cancellationToken);

        var evidence = await dbContext.Evidence.AsNoTracking()
            .Where(e => e.ExecutionId == executionId)
            .ToListAsync(cancellationToken);

        // Compose report, preserving deterministic findings and system envelope
        var contractReport = ReportComposer.Compose(
            execution,
            evaluations,
            findings,
            evidence,
            analyticContent,
            usageSummary);

        // Prepare sanitized fields for Domain Report entity
        var sanitizedSummary = await sanitizer.SanitizeAsync(executionId, contractReport.Summary, cancellationToken: cancellationToken);

        MaskedText? sanitizedAnalyticContent = null;
        if (contractReport.Analysis is not null)
        {
            var analysisJson = ContractJson.Serialize(contractReport.Analysis);
            sanitizedAnalyticContent = await sanitizer.SanitizeAsync(executionId, analysisJson, cancellationToken: cancellationToken);
        }

        var reportEntity = await dbContext.Reports
            .SingleOrDefaultAsync(r => r.ExecutionId == executionId, cancellationToken);

        if (reportEntity is null)
        {
            reportEntity = new Domain.Reports.Report(
                executionId,
                sanitizedSummary,
                clock.GetUtcNow(),
                sanitizedAnalyticContent);

            dbContext.Reports.Add(reportEntity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return contractReport;
    }
}
