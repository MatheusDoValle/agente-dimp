using Api.Agente.Application.Contracts;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Xunit;

namespace Api.Agente.UnitTests.Reports;

public class OperationalEnvelopeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid ExecId = Guid.NewGuid();

    [Fact]
    public void ReportComposer_PreservesSystemEnvelope_IgnoringModelManipulations()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);
        exec.SetReceivedFile(1000, "ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890");
        exec.SetVerdict(ExecutionVerdict.WithInconsistencies, requiresHumanReview: true);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Failed)
        };

        var maskedDesc = new MaskedText(ExecId, "Inconsistencia detectada.");
        var evidenceSnapshot = new EvidenceSnapshot(ExecId, EvidenceSource.MaskedFile, new MaskedText(ExecId, "Linha com erro"));
        var finding = new Api.Agente.Domain.Validation.Finding(
            ExecId,
            "DIMP10.R0000.F01",
            FindingCategory.BusinessInconsistency,
            FindingSeverity.Error,
            FindingOrigin.Deterministic,
            maskedDesc,
            new DocumentLocation(1, 0, "0000", 1, null),
            [evidenceSnapshot.Id]);

        // Model attempts to claim that everything passed, no human review needed, different executionId and invented usage
        var analyticContent = new AnalyticContent
        {
            Summary = "Modelo diz que tudo esta perfeito e sem erros.",
            Explanations =
            [
                new AnalyticContentExplanationsItem
                {
                    FindingIds = [finding.Id.ToString()],
                    Text = "Explicacao analitica do achado.",
                    EvidenceIds = [evidenceSnapshot.Id.ToString()]
                }
            ],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = false // Model tries to lower human review!
        };

        var usage = new UsageSummary
        {
            InputTokens = 150,
            OutputTokens = 50,
            EmbeddingTokens = null,
            HasUnknownUsage = false,
            EstimatedCost = "0.002",
            Currency = "USD",
            PriceBookVersionId = "pb-2026-10"
        };

        var report = ReportComposer.Compose(
            exec,
            evaluations,
            [finding],
            [evidenceSnapshot],
            analyticContent,
            usage);

        // System envelope is strictly preserved:
        Assert.Equal(ExecId, report.ExecutionId);
        Assert.Equal(ReportVerdictValue.ComInconsistencias, report.Verdict);
        Assert.Equal(1L, report.FindingsCount);
        Assert.Equal(1L, report.Coverage.Failed);
        Assert.True(report.RequiresHumanReview); // System enforced true, model could NOT lower it!

        // Usage comes from system, not invented by model
        Assert.Equal(150L, report.Usage.InputTokens);
        Assert.Equal("0.002", report.Usage.EstimatedCost);

        // Analysis content was attached safely
        Assert.NotNull(report.Analysis);
        Assert.Equal("Modelo diz que tudo esta perfeito e sem erros.", report.Analysis.Summary);
    }

    [Fact]
    public void ReportComposer_PreventsLoweringOfSystemHumanReview()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);
        exec.SetVerdict(ExecutionVerdict.Inconclusive, requiresHumanReview: true);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Undetermined, RuleResult.BlockedBySourceConflict)
        };

        // Model returns false for human review
        var analyticContent = new AnalyticContent
        {
            Summary = "Resumo do modelo.",
            Explanations = [],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = false
        };

        var report = ReportComposer.Compose(
            exec,
            evaluations,
            [],
            [],
            analyticContent,
            null);

        Assert.True(report.RequiresHumanReview);
    }

    [Fact]
    public void ReportComposer_RaisesHumanReview_WhenModelIdentifiesNewLimitationOrIndication()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);
        exec.SetVerdict(ExecutionVerdict.NoInconsistenciesInEvaluatedRules, requiresHumanReview: false);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed)
        };

        // Model requests human review because of ambiguous semantic interpretation
        var analyticContent = new AnalyticContent
        {
            Summary = "Resumo do modelo com duvida analitica.",
            Explanations = [],
            Indications = [],
            Limitations = ["Possivel ambiguidade no texto complementar."],
            RequiresHumanReview = true
        };

        var report = ReportComposer.Compose(
            exec,
            evaluations,
            [],
            [],
            analyticContent,
            null);

        // System respects model's request to escalate to human review
        Assert.True(report.RequiresHumanReview);
    }

    [Fact]
    public void ReportComposer_FiltersOutInvalidOrForeignReferencesFromModelExplanations()
    {
        var exec = new Execution(ExecId, Guid.NewGuid(), "op", "ctx", Now);
        exec.Start(Now);

        var evaluations = new List<RuleEvaluation>
        {
            new(ExecId, "DIMP10.R0000.F01", "line:1", RuleApplicability.Applicable, RuleResult.Passed)
        };

        var foreignFindingId = Guid.NewGuid().ToString();
        var foreignEvidenceId = Guid.NewGuid().ToString();

        var analyticContent = new AnalyticContent
        {
            Summary = "Modelo tenta inventar referencias.",
            Explanations =
            [
                new AnalyticContentExplanationsItem
                {
                    FindingIds = [foreignFindingId],
                    Text = "Explicacao de achado que nao existe.",
                    EvidenceIds = [foreignEvidenceId]
                }
            ],
            Indications =
            [
                new AnalyticContentIndicationsItem
                {
                    Text = "Indicio referenciando evidencia inexistente.",
                    EvidenceIds = [foreignEvidenceId]
                }
            ],
            Limitations = [],
            RequiresHumanReview = false
        };

        var report = ReportComposer.Compose(
            exec,
            evaluations,
            [],
            [],
            analyticContent,
            null);

        // Non-existent finding/evidence IDs must be sanitized/filtered out from explanations
        Assert.NotNull(report.Analysis);
        Assert.Empty(report.Analysis.Explanations); // Filtered out because findingId does not exist
        Assert.Empty(report.Analysis.Indications[0].EvidenceIds); // Filtered out foreign evidenceId
    }
}
