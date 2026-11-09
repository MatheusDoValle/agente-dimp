using Api.Agente.Domain.Validation;

namespace Api.Agente.Domain.Reports;

public sealed record CoverageSummary(
    string CatalogVersion,
    long Applicable,
    long Passed,
    long Failed,
    long NotExecuted,
    long BlockedBySourceConflict);

public static class ReportPolicies
{
    public const string CategoryStructuralErrorWire = "erro_estrutural";
    public const string CategoryBusinessInconsistencyWire = "inconsistencia_negocio";
    public const string CategoryAiIndicationWire = "indicio_ia";
    public const string CategoryMissingEvidenceWire = "ausencia_evidencias";
    public const string CategoryTechnicalFailureWire = "falha_tecnica";

    public const string SeverityBlockingWire = "bloqueante";
    public const string SeverityErrorWire = "erro";
    public const string SeverityWarningWire = "aviso";
    public const string SeverityInformationWire = "informativo";

    public static string ToWireCategory(FindingCategory category) => category switch
    {
        FindingCategory.StructuralError => CategoryStructuralErrorWire,
        FindingCategory.BusinessInconsistency => CategoryBusinessInconsistencyWire,
        FindingCategory.AiIndication => CategoryAiIndicationWire,
        FindingCategory.MissingEvidence => CategoryMissingEvidenceWire,
        FindingCategory.TechnicalFailure => CategoryTechnicalFailureWire,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Categoria desconhecida.")
    };

    public static string ToWireSeverity(FindingSeverity severity) => severity switch
    {
        FindingSeverity.Blocking => SeverityBlockingWire,
        FindingSeverity.Error => SeverityErrorWire,
        FindingSeverity.Warning => SeverityWarningWire,
        FindingSeverity.Information => SeverityInformationWire,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Severidade desconhecida.")
    };

    public static CoverageSummary ComputeCoverage(string catalogVersion, IEnumerable<RuleEvaluation> evaluations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogVersion);
        ArgumentNullException.ThrowIfNull(evaluations);

        long passed = 0;
        long failed = 0;
        long notExecuted = 0;
        long blockedBySourceConflict = 0;

        foreach (var ev in evaluations)
        {
            if (ev.Applicability == RuleApplicability.NotApplicable)
                continue;

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

        var applicable = passed + failed + notExecuted + blockedBySourceConflict;

        return new CoverageSummary(
            catalogVersion,
            applicable,
            passed,
            failed,
            notExecuted,
            blockedBySourceConflict);
    }

    public static bool DetermineRequiresHumanReview(
        Execution execution,
        CoverageSummary coverage,
        IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(findings);

        if (execution.RequiresHumanReview)
            return true;

        if (coverage.Failed > 0 || coverage.BlockedBySourceConflict > 0 || coverage.NotExecuted > 0)
            return true;

        foreach (var finding in findings)
        {
            if (finding.Category is FindingCategory.AiIndication
                or FindingCategory.MissingEvidence
                or FindingCategory.TechnicalFailure)
            {
                return true;
            }

            if (finding.Severity == FindingSeverity.Blocking)
            {
                return true;
            }
        }

        return false;
    }

    public static ExecutionVerdict DetermineVerdict(
        Execution execution,
        CoverageSummary coverage,
        IEnumerable<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(findings);

        if (execution.Verdict == ExecutionVerdict.NotSupported)
            return ExecutionVerdict.NotSupported;

        if (coverage.Failed > 0 || findings.Any(f => f.Category is FindingCategory.StructuralError or FindingCategory.BusinessInconsistency))
            return ExecutionVerdict.WithInconsistencies;

        if (coverage.BlockedBySourceConflict > 0 || coverage.NotExecuted > 0 || findings.Any(f => f.Category is FindingCategory.MissingEvidence or FindingCategory.TechnicalFailure))
            return ExecutionVerdict.Inconclusive;

        if (execution.Verdict == ExecutionVerdict.NoInconsistenciesInEvaluatedRules)
            return ExecutionVerdict.NoInconsistenciesInEvaluatedRules;

        if (coverage.Applicable > 0 && coverage.Passed == coverage.Applicable)
            return ExecutionVerdict.NoInconsistenciesInEvaluatedRules;

        if (execution.Status == ExecutionState.Completed && coverage.Failed == 0 && !findings.Any() && coverage.BlockedBySourceConflict == 0 && coverage.NotExecuted == 0)
            return ExecutionVerdict.NoInconsistenciesInEvaluatedRules;

        return execution.Verdict;
    }
}
