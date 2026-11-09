using Api.Agente.Domain;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Xunit;

namespace Api.Agente.UnitTests.Reports;

public class ReportPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid ExecId = Guid.NewGuid();

    [Fact]
    public void Coverage_AggregatesStrictly_WithExclusiveCategories()
    {
        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
            new(ExecId, "DIMP10.R0100.F02", "line:2", RuleApplicability.Applicable, RuleResult.Failed),
            new(ExecId, "DIMP10.R0200.F03", "line:3", RuleApplicability.Undetermined, RuleResult.NotExecuted),
            new(ExecId, "DIMP10.R0300.F04", "line:4", RuleApplicability.Undetermined, RuleResult.BlockedBySourceConflict),
            new(ExecId, "DIMP10.R0400.F05", "line:5", RuleApplicability.NotApplicable, RuleResult.NotApplicable)
        };

        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        Assert.Equal("dimp-v10", coverage.CatalogVersion);
        Assert.Equal(4, coverage.Applicable);
        Assert.Equal(1, coverage.Passed);
        Assert.Equal(1, coverage.Failed);
        Assert.Equal(1, coverage.NotExecuted);
        Assert.Equal(1, coverage.BlockedBySourceConflict);

        // Invariant: applicable == passed + failed + notExecuted + blockedBySourceConflict
        Assert.Equal(coverage.Applicable, coverage.Passed + coverage.Failed + coverage.NotExecuted + coverage.BlockedBySourceConflict);
    }

    [Fact]
    public void Coverage_SameRuleWithMultipleOccurrences_AggregatesEachInstance()
    {
        var ruleId = "DIMP10.R0100.F02";
        var evaluations = new List<RuleEvaluation>
        {
            // Occurrence 1: Valid
            new(ExecId, ruleId, "line:10", RuleApplicability.Applicable, RuleResult.Passed),
            // Occurrence 2: Inconsistent
            new(ExecId, ruleId, "line:25", RuleApplicability.Applicable, RuleResult.Failed),
            // Occurrence 3: Valid
            new(ExecId, ruleId, "line:40", RuleApplicability.Applicable, RuleResult.Passed)
        };

        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        Assert.Equal(3, coverage.Applicable);
        Assert.Equal(2, coverage.Passed);
        Assert.Equal(1, coverage.Failed);
        Assert.Equal(0, coverage.NotExecuted);
        Assert.Equal(0, coverage.BlockedBySourceConflict);
        Assert.Equal(coverage.Applicable, coverage.Passed + coverage.Failed + coverage.NotExecuted + coverage.BlockedBySourceConflict);
    }

    [Fact]
    public void CategoriesAndSeverities_WireNamesMatchSpecification()
    {
        Assert.Equal("erro_estrutural", ReportPolicies.ToWireCategory(FindingCategory.StructuralError));
        Assert.Equal("inconsistencia_negocio", ReportPolicies.ToWireCategory(FindingCategory.BusinessInconsistency));
        Assert.Equal("indicio_ia", ReportPolicies.ToWireCategory(FindingCategory.AiIndication));
        Assert.Equal("ausencia_evidencias", ReportPolicies.ToWireCategory(FindingCategory.MissingEvidence));
        Assert.Equal("falha_tecnica", ReportPolicies.ToWireCategory(FindingCategory.TechnicalFailure));

        Assert.Equal("bloqueante", ReportPolicies.ToWireSeverity(FindingSeverity.Blocking));
        Assert.Equal("erro", ReportPolicies.ToWireSeverity(FindingSeverity.Error));
        Assert.Equal("aviso", ReportPolicies.ToWireSeverity(FindingSeverity.Warning));
        Assert.Equal("informativo", ReportPolicies.ToWireSeverity(FindingSeverity.Information));
    }

    [Fact]
    public void Invariant_AbsenceOfEvidenceOrTechnicalFailure_DoesNotBecomeBusinessError()
    {
        var maskedDesc = new MaskedText(ExecId, "Manual indisponivel no momento.");
        var location = new DocumentLocation(1, 0, "0000", null, null);

        // MissingEvidence remains MissingEvidence even with Error severity
        var finding = new Finding(ExecId, null, FindingCategory.MissingEvidence, FindingSeverity.Error,
            FindingOrigin.System, maskedDesc, location, []);

        Assert.Equal(FindingCategory.MissingEvidence, finding.Category);
        Assert.NotEqual(FindingCategory.BusinessInconsistency, finding.Category);
    }

    [Fact]
    public void HumanReview_RequiredWhenViolationsExist_EvenIfStateIsCompleted()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Failed)
        };
        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        var maskedDesc = new MaskedText(ExecId, "Erro de formato.");
        var evidenceId = Guid.NewGuid();
        var finding = new Finding(ExecId, "DIMP10.R0000.F01", FindingCategory.BusinessInconsistency,
            FindingSeverity.Error, FindingOrigin.Deterministic, maskedDesc, new DocumentLocation(1, 0, "0000", 1, null), [evidenceId]);

        var requiresReview = ReportPolicies.DetermineRequiresHumanReview(exec, coverage, [finding]);
        var verdict = ReportPolicies.DetermineVerdict(exec, coverage, [finding]);

        Assert.True(requiresReview);
        Assert.Equal(ExecutionVerdict.WithInconsistencies, verdict);
    }

    [Fact]
    public void HumanReview_RequiredWhenSourceConflictExists()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
            new(ExecId, "DIMP10.R0200.F03", "line:2", RuleApplicability.Undetermined, RuleResult.BlockedBySourceConflict)
        };
        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        var requiresReview = ReportPolicies.DetermineRequiresHumanReview(exec, coverage, []);
        var verdict = ReportPolicies.DetermineVerdict(exec, coverage, []);

        Assert.True(requiresReview);
        Assert.Equal(ExecutionVerdict.Inconclusive, verdict);
    }

    [Fact]
    public void HumanReview_RequiredWhenMandatoryValidationNotExecuted()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
            new(ExecId, "DIMP10.R0100.F01", "line:2", RuleApplicability.Undetermined, RuleResult.NotExecuted)
        };
        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        var requiresReview = ReportPolicies.DetermineRequiresHumanReview(exec, coverage, []);
        var verdict = ReportPolicies.DetermineVerdict(exec, coverage, []);

        Assert.True(requiresReview);
        Assert.Equal(ExecutionVerdict.Inconclusive, verdict);
    }

    [Fact]
    public void HumanReview_RequiredWhenAiIndicationOrUnprovenFindingPresent()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed)
        };
        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        var maskedDesc = new MaskedText(ExecId, "Indicio de discrepancia.");
        var finding = new Finding(ExecId, null, FindingCategory.AiIndication, FindingSeverity.Warning,
            FindingOrigin.Ai, maskedDesc, new DocumentLocation(1, 0, "0000", null, null), []);

        var requiresReview = ReportPolicies.DetermineRequiresHumanReview(exec, coverage, [finding]);

        Assert.True(requiresReview);
    }

    [Fact]
    public void HumanReview_NotRequiredWhenExecutionCleanAndAllEvaluatedRulesPass()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
            new(ExecId, "DIMP10.R0100.F01", "line:2", RuleApplicability.Applicable, RuleResult.Passed)
        };
        var coverage = ReportPolicies.ComputeCoverage("dimp-v10", evaluations);

        var requiresReview = ReportPolicies.DetermineRequiresHumanReview(exec, coverage, []);
        var verdict = ReportPolicies.DetermineVerdict(exec, coverage, []);

        Assert.False(requiresReview);
        Assert.Equal(ExecutionVerdict.NoInconsistenciesInEvaluatedRules, verdict);
    }
}
