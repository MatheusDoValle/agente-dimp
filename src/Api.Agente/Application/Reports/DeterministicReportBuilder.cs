using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;

namespace Api.Agente.Application.Reports;

public static class DeterministicReportBuilder
{
    public static Contracts.Report Build(
        Execution execution,
        IReadOnlyList<RuleEvaluation> evaluations,
        IReadOnlyList<Domain.Validation.Finding> findings,
        IReadOnlyList<EvidenceSnapshot> evidenceSnapshots)
    {
        var catalogVersion = execution.RuleCatalogVersion ?? "dimp-v10";

        long applicable = 0;
        long passed = 0;
        long failed = 0;
        long notExecuted = 0;
        long blockedBySourceConflict = 0;

        foreach (var ev in evaluations)
        {
            if (ev.Applicability != RuleApplicability.NotApplicable)
                applicable++;

            switch (ev.Result)
            {
                case RuleResult.Passed:
                    passed++;
                    break;
                case RuleResult.Failed:
                    failed++;
                    break;
                case RuleResult.NotExecuted:
                    notExecuted++;
                    break;
                case RuleResult.BlockedBySourceConflict:
                    blockedBySourceConflict++;
                    break;
            }
        }

        var coverage = new Coverage
        {
            CatalogVersion = catalogVersion,
            Applicable = applicable,
            Passed = passed,
            Failed = failed,
            NotExecuted = notExecuted,
            BlockedBySourceConflict = blockedBySourceConflict
        };

        var status = execution.Status switch
        {
            ExecutionState.Completed => ReportStatusValue.Concluida,
            ExecutionState.PartiallyCompleted => ReportStatusValue.ConcluidaParcialmente,
            ExecutionState.Cancelled => ReportStatusValue.Cancelada,
            _ => ReportStatusValue.Falhou
        };

        var verdict = execution.Verdict switch
        {
            ExecutionVerdict.NoInconsistenciesInEvaluatedRules => ReportVerdictValue.SemInconsistenciasNasRegrasAvaliadas,
            ExecutionVerdict.WithInconsistencies => ReportVerdictValue.ComInconsistencias,
            ExecutionVerdict.NotSupported => ReportVerdictValue.NaoSuportado,
            _ => ReportVerdictValue.Inconclusivo
        };

        var summary = failed > 0
            ? $"Validacao deterministica concluida: {failed} inconsistencia(s) identificada(s)."
            : "Validacao deterministica concluida sem inconsistencias nas regras avaliadas.";

        var evidenceList = evidenceSnapshots.Select(e => new Contracts.Evidence
        {
            Id = e.Id.ToString(),
            SourceKind = e.Source switch
            {
                EvidenceSource.Manual => EvidenceSourceKindValue.Manual,
                EvidenceSource.MaskedFile => EvidenceSourceKindValue.ArquivoMascarado,
                _ => EvidenceSourceKindValue.Execucao
            },
            RevisionSha256 = e.RevisionSha256,
            ChunkId = e.ChunkId,
            Pages = e.Pages.Select(p => (long)p).ToArray(),
            Locator = e.Locator,
            Text = e.Text
        }).ToArray();

        var endedAt = execution.EndedAt ?? execution.UpdatedAt;
        var expiresAt = execution.ReportExpiresAt ?? endedAt.AddDays(30);

        var limitationCodes = execution.LimitationCodes?.ToList() ?? new List<string>();
        if (blockedBySourceConflict > 0 && !limitationCodes.Contains("BLOCKED_BY_SOURCE_CONFLICT"))
        {
            limitationCodes.Add("BLOCKED_BY_SOURCE_CONFLICT");
        }

        return new Contracts.Report
        {
            ContractVersion = ReportContractVersionValue.Version100,
            ExecutionId = execution.Id,
            Status = status,
            Verdict = verdict,
            Coverage = coverage,
            Summary = summary,
            Analysis = null,
            RequiresHumanReview = execution.RequiresHumanReview || failed > 0 || blockedBySourceConflict > 0,
            LimitationCodes = limitationCodes,
            FindingsCount = findings.Count,
            FindingsUrl = $"/api/v1/analises/{execution.Id}/achados",
            Evidence = evidenceList,
            Usage = new UsageSummary
            {
                InputTokens = null,
                OutputTokens = null,
                EmbeddingTokens = null,
                HasUnknownUsage = false,
                EstimatedCost = null,
                Currency = null,
                PriceBookVersionId = null
            },
            EndedAt = endedAt,
            ExpiresAt = expiresAt
        };
    }
}
