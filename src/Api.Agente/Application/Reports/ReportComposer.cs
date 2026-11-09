using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;

namespace Api.Agente.Application.Reports;

public static class ReportComposer
{
    public static Contracts.Report Compose(
        Execution execution,
        IReadOnlyList<RuleEvaluation> evaluations,
        IReadOnlyList<Domain.Validation.Finding> findings,
        IReadOnlyList<EvidenceSnapshot> evidenceSnapshots,
        AnalyticContent? analyticContent = null,
        UsageSummary? usageSummary = null)
    {
        ArgumentNullException.ThrowIfNull(execution);
        evaluations ??= [];
        findings ??= [];
        evidenceSnapshots ??= [];

        var catalogVersion = execution.RuleCatalogVersion ?? "dimp-v10";
        var coverageSummary = ReportPolicies.ComputeCoverage(catalogVersion, evaluations);

        var coverage = new Coverage
        {
            CatalogVersion = coverageSummary.CatalogVersion,
            Applicable = coverageSummary.Applicable,
            Passed = coverageSummary.Passed,
            Failed = coverageSummary.Failed,
            NotExecuted = coverageSummary.NotExecuted,
            BlockedBySourceConflict = coverageSummary.BlockedBySourceConflict
        };

        var systemStatus = execution.Status switch
        {
            ExecutionState.Completed => ReportStatusValue.Concluida,
            ExecutionState.PartiallyCompleted => ReportStatusValue.ConcluidaParcialmente,
            ExecutionState.Cancelled => ReportStatusValue.Cancelada,
            _ => ReportStatusValue.Falhou
        };

        var determinedVerdict = ReportPolicies.DetermineVerdict(execution, coverageSummary, findings);
        var reportVerdict = determinedVerdict switch
        {
            ExecutionVerdict.NoInconsistenciesInEvaluatedRules => ReportVerdictValue.SemInconsistenciasNasRegrasAvaliadas,
            ExecutionVerdict.WithInconsistencies => ReportVerdictValue.ComInconsistencias,
            ExecutionVerdict.NotSupported => ReportVerdictValue.NaoSuportado,
            _ => ReportVerdictValue.Inconclusivo
        };

        var systemRequiresReview = ReportPolicies.DetermineRequiresHumanReview(execution, coverageSummary, findings);
        var finalRequiresReview = systemRequiresReview || (analyticContent?.RequiresHumanReview ?? false);

        var limitationCodes = new List<string>(execution.LimitationCodes ?? []);
        if (coverageSummary.BlockedBySourceConflict > 0 && !limitationCodes.Contains("BLOCKED_BY_SOURCE_CONFLICT"))
        {
            limitationCodes.Add("BLOCKED_BY_SOURCE_CONFLICT");
        }
        if (coverageSummary.NotExecuted > 0 && !limitationCodes.Contains("VALIDATION_INCOMPLETE"))
        {
            limitationCodes.Add("VALIDATION_INCOMPLETE");
        }
        if (analyticContent?.Limitations is not null)
        {
            foreach (var lim in analyticContent.Limitations)
            {
                if (!string.IsNullOrWhiteSpace(lim) && !limitationCodes.Contains(lim))
                {
                    limitationCodes.Add(lim);
                }
            }
        }

        var deterministicSummary = coverageSummary.Failed > 0
            ? $"Validacao deterministica concluida: {coverageSummary.Failed} inconsistencia(s) identificada(s)."
            : "Validacao deterministica concluida sem inconsistencias nas regras avaliadas.";

        var summary = deterministicSummary;

        var sanitizedAnalysis = SanitizeAnalyticContent(analyticContent, findings, evidenceSnapshots);

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

        var usage = usageSummary ?? new UsageSummary
        {
            InputTokens = null,
            OutputTokens = null,
            EmbeddingTokens = null,
            HasUnknownUsage = false,
            EstimatedCost = null,
            Currency = null,
            PriceBookVersionId = null
        };

        return new Contracts.Report
        {
            ContractVersion = ReportContractVersionValue.Version100,
            ExecutionId = execution.Id,
            Status = systemStatus,
            Verdict = reportVerdict,
            Coverage = coverage,
            Summary = summary,
            Analysis = sanitizedAnalysis,
            RequiresHumanReview = finalRequiresReview,
            LimitationCodes = limitationCodes,
            FindingsCount = findings.Count,
            FindingsUrl = $"/api/v1/analises/{execution.Id}/achados",
            Evidence = evidenceList,
            Usage = usage,
            EndedAt = endedAt,
            ExpiresAt = expiresAt
        };
    }

    private static AnalyticContent? SanitizeAnalyticContent(
        AnalyticContent? analysis,
        IReadOnlyList<Domain.Validation.Finding> findings,
        IReadOnlyList<EvidenceSnapshot> evidenceSnapshots)
    {
        if (analysis is null)
            return null;

        var existingFindingIds = findings.Select(f => f.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEvidenceIds = evidenceSnapshots.Select(e => e.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sanitizedExplanations = new List<AnalyticContentExplanationsItem>();
        if (analysis.Explanations is not null)
        {
            foreach (var explanation in analysis.Explanations)
            {
                var validFindingIds = explanation.FindingIds.Where(id => existingFindingIds.Contains(id)).ToArray();
                if (validFindingIds.Length == 0)
                {
                    // If explanation references zero valid findings belonging to this execution, skip it
                    continue;
                }

                var validEvidenceIds = explanation.EvidenceIds.Where(id => existingEvidenceIds.Contains(id)).ToArray();

                sanitizedExplanations.Add(new AnalyticContentExplanationsItem
                {
                    FindingIds = validFindingIds,
                    Text = explanation.Text,
                    EvidenceIds = validEvidenceIds
                });
            }
        }

        var sanitizedIndications = new List<AnalyticContentIndicationsItem>();
        if (analysis.Indications is not null)
        {
            foreach (var indication in analysis.Indications)
            {
                var validEvidenceIds = indication.EvidenceIds.Where(id => existingEvidenceIds.Contains(id)).ToArray();
                sanitizedIndications.Add(new AnalyticContentIndicationsItem
                {
                    Text = indication.Text,
                    EvidenceIds = validEvidenceIds
                });
            }
        }

        return new AnalyticContent
        {
            Summary = analysis.Summary,
            Explanations = sanitizedExplanations,
            Indications = sanitizedIndications,
            Limitations = analysis.Limitations ?? [],
            RequiresHumanReview = analysis.RequiresHumanReview
        };
    }
}
