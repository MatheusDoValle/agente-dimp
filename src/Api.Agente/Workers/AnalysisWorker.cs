using Api.Agente.Application.Contracts;
using Api.Agente.Application.Lifecycle;
using Api.Agente.Application.Privacy;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.Dimp;
using Api.Agente.Infrastructure.Jobs;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Workers;

public sealed class AnalysisWorker(
    IServiceScopeFactory scopeFactory,
    IJobStore jobStore,
    ITemporaryArtifactStore artifactStore,
    TimeProvider clock,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"worker-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            JobLease? lease = null;
            try
            {
                lease = await jobStore.TryLeaseNextAsync(_workerId, TimeSpan.FromMinutes(2), clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Erro ao obter lease de trabalho.");
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            if (lease is null)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            try
            {
                await ProcessJobAsync(lease, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao processar execucao {ExecutionId}", lease.ExecutionId);
                await TryFailExecutionAsync(lease.ExecutionId);
                await jobStore.CompleteJobAsync(lease.ExecutionId, _workerId, lease.FencingToken, clock.GetUtcNow(), CancellationToken.None);
            }
        }
    }

    private async Task ProcessJobAsync(JobLease lease, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        var finalizer = scope.ServiceProvider.GetRequiredService<ExecutionFinalizer>();
        var sanitizer = scope.ServiceProvider.GetRequiredService<ContentSanitizer>();

        var execution = await db.Executions.SingleOrDefaultAsync(e => e.Id == lease.ExecutionId, cancellationToken);
        if (execution is null || execution.EndedAt.HasValue || Execution.IsTerminal(execution.Status))
        {
            await jobStore.CompleteJobAsync(lease.ExecutionId, _workerId, lease.FencingToken, clock.GetUtcNow(), cancellationToken);
            return;
        }

        // 1. Iniciar execucao
        execution.Start(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        // 2. Etapa de Identificacao
        await jobStore.UpdateCheckpointAsync(lease.ExecutionId, _workerId, lease.FencingToken, "identification", null, clock.GetUtcNow(), cancellationToken);

        var artifacts = await artifactStore.ListAsync(lease.ExecutionId, cancellationToken);
        var originalArtifact = artifacts.FirstOrDefault(a => a.Kind == TemporaryArtifactKind.Original);
        if (originalArtifact is null)
        {
            throw new InvalidOperationException("Artefato original ausente para execucao.");
        }

        DimpLayoutResolution layout;
        await using (var initialStream = await artifactStore.OpenReadAsync(lease.ExecutionId, originalArtifact.ArtifactId, cancellationToken))
        {
            var parser = new DimpParser();
            DimpRecord? firstRecord = null;
            await foreach (var r in parser.ReadAsync(initialStream, cancellationToken))
            {
                firstRecord = r;
                break;
            }

            if (firstRecord is null)
            {
                throw new InvalidOperationException("Arquivo sem registros para identificacao de layout.");
            }

            var resolver = new LayoutResolver();
            layout = resolver.Resolve(firstRecord);
        }

        if (!layout.Supported || layout.LayoutVersion != "10")
        {
            execution.SetVerdict(ExecutionVerdict.NotSupported, true);
            execution.SetLimitations(["UNSUPPORTED_LAYOUT"]);
            await jobStore.BeginCompletingAsync(lease.ExecutionId, _workerId, lease.FencingToken, clock.GetUtcNow(), cancellationToken);
            await finalizer.FinalizeAsync(lease.ExecutionId, ExecutionState.Failed, cancellationToken);
            await jobStore.CompleteJobAsync(lease.ExecutionId, _workerId, lease.FencingToken, clock.GetUtcNow(), cancellationToken);
            return;
        }

        var execContext = scope.ServiceProvider.GetRequiredService<Api.Agente.Application.ExecutionContext>();
        var configSnapshot = execContext.CreateConfigurationSnapshot();

        execution.SetSource(layout.LayoutVersion, layout.RevisionSha256!, RuleCatalog.SupportedVersion, configSnapshot.Id);
        await db.SaveChangesAsync(cancellationToken);

        // 3. Etapa de Validacao
        execution.MoveTo(ExecutionStage.Validation, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await jobStore.UpdateCheckpointAsync(lease.ExecutionId, _workerId, lease.FencingToken, "validation", null, clock.GetUtcNow(), cancellationToken);

        var storageOptions = scope.ServiceProvider.GetRequiredService<TemporaryStorageOptions>();
        var dbPath = Path.Combine(storageOptions.RootPath, lease.ExecutionId.ToString("N"), $"{Guid.NewGuid():N}.sqlite");

        var evaluations = new List<RuleEvaluation>();
        await using (var index = await TemporaryValidationIndex.CreateAsync(dbPath, cancellationToken))
        {
            await using (var parseStream = await artifactStore.OpenReadAsync(lease.ExecutionId, originalArtifact.ArtifactId, cancellationToken))
            {
                var parser = new DimpParser();
                var engine = new DimpValidationEngine();
                var records = parser.ReadAsync(parseStream, cancellationToken);

                await foreach (var eval in engine.ValidateAsync(records, index, lease.ExecutionId, cancellationToken))
                {
                    evaluations.Add(eval);
                }
            }
        }

        evaluations = evaluations.GroupBy(e => (e.RuleId, e.OccurrenceKey)).Select(g => g.First()).ToList();
        db.RuleEvaluations.AddRange(evaluations);

        var hasFailures = evaluations.Any(e => e.Result == RuleResult.Failed);
        var hasConflicts = evaluations.Any(e => e.Result == RuleResult.BlockedBySourceConflict);
        execution.SetVerdict(
            hasFailures ? ExecutionVerdict.WithInconsistencies : ExecutionVerdict.NoInconsistenciesInEvaluatedRules,
            hasConflicts);

        var findings = new List<Domain.Validation.Finding>();
        foreach (var ev in evaluations.Where(e => e.Result == RuleResult.Failed))
        {
            long? line = null;
            long? field = null;
            if (ev.OccurrenceKey.StartsWith("line:"))
            {
                var parts = ev.OccurrenceKey[5..].Split("/field:");
                if (long.TryParse(parts[0], out var l)) line = l;
                if (parts.Length > 1 && long.TryParse(parts[1], out var f)) field = f;
            }

            var docLoc = line.HasValue
                ? new DocumentLocation(line.Value, 0, null, field, null)
                : new DocumentLocation(null, null, null, null, "Arquivo");

            var evText = await sanitizer.SanitizeAsync(execution.Id, $"Inconsistencia na regra {ev.RuleId} em {ev.OccurrenceKey}", cancellationToken: cancellationToken);
            var evLoc = await sanitizer.SanitizeAsync(execution.Id, ev.OccurrenceKey, cancellationToken: cancellationToken);
            var evidence = new EvidenceSnapshot(execution.Id, EvidenceSource.MaskedFile, evText, locator: evLoc);
            db.Evidence.Add(evidence);

            var desc = await sanitizer.SanitizeAsync(execution.Id, $"Inconsistencia na regra {ev.RuleId}", cancellationToken: cancellationToken);
            var finding = new Domain.Validation.Finding(
                execution.Id,
                ev.RuleId,
                FindingCategory.BusinessInconsistency,
                FindingSeverity.Error,
                FindingOrigin.Deterministic,
                desc,
                docLoc,
                [evidence.Id]);

            findings.Add(finding);
            db.Findings.Add(finding);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 4. Etapa de Composicao
        execution.MoveTo(ExecutionStage.Composition, clock.GetUtcNow());

        var contextualStage = scope.ServiceProvider.GetService<Api.Agente.Application.AI.IContextualAnalysisStage>();
        AnalyticContent? analyticContent = null;
        var aiEnabled = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("AiAnalysis:Enabled", false);
        if (aiEnabled && contextualStage != null)
        {
            analyticContent = await contextualStage.ExecuteAsync(
                execution.Id,
                execution.LayoutVersion ?? "10",
                execution.DocumentRevisionSha256 ?? "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
                "text-embedding-3-small-1536",
                findings,
                execution.RequiresHumanReview,
                cancellationToken);
        }

        var limitations = new List<string>();
        if (analyticContent == null) limitations.Add("AI_ANALYSIS_SKIPPED");
        if (hasConflicts) limitations.Add("BLOCKED_BY_SOURCE_CONFLICT");
        if (analyticContent?.Limitations != null) limitations.AddRange(analyticContent.Limitations);
        execution.SetLimitations(limitations.Distinct().ToList());
        await db.SaveChangesAsync(cancellationToken);

        var usageAggregator = scope.ServiceProvider.GetService<Api.Agente.Application.Usage.IExecutionUsageAggregator>();
        UsageSummary? usageSummary = null;
        if (usageAggregator != null)
        {
            usageSummary = await usageAggregator.AggregateExecutionUsageAsync(execution.Id, null, cancellationToken);
        }

        var reportBuilder = scope.ServiceProvider.GetRequiredService<BuildExecutionReport>();
        await reportBuilder.ExecuteAsync(execution.Id, analyticContent, usageSummary, cancellationToken);

        // 5. Etapa de Limpeza e Finalizacao
        await jobStore.BeginCompletingAsync(lease.ExecutionId, _workerId, lease.FencingToken, clock.GetUtcNow(), cancellationToken);
        var terminalState = hasConflicts ? ExecutionState.PartiallyCompleted : ExecutionState.Completed;
        await finalizer.FinalizeAsync(lease.ExecutionId, terminalState, cancellationToken);
        await jobStore.CompleteJobAsync(lease.ExecutionId, _workerId, lease.FencingToken, clock.GetUtcNow(), cancellationToken);
    }

    private async Task TryFailExecutionAsync(Guid executionId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var finalizer = scope.ServiceProvider.GetRequiredService<ExecutionFinalizer>();
            await finalizer.FinalizeAsync(executionId, ExecutionState.Failed, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao registrar encerramento por erro da execucao {ExecutionId}", executionId);
        }
    }
}
