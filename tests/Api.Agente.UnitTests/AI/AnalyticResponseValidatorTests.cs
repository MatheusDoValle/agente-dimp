using Api.Agente.Application.AI;
using Api.Agente.Application.Contracts;
using Xunit;

namespace Api.Agente.UnitTests.AI;

public class AnalyticResponseValidatorTests
{
    [Fact]
    public void Validate_WithLegitimateIds_Succeeds()
    {
        var findingId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();

        var content = new AnalyticContent
        {
            Summary = "Analise conforme.",
            Explanations =
            [
                new AnalyticContentExplanationsItem
                {
                    FindingIds = [findingId.ToString()],
                    EvidenceIds = [evidenceId.ToString()],
                    Text = "Explicacao consistente com o manual."
                }
            ],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = false
        };

        var validator = new AnalyticResponseValidator();
        var outcome = validator.Validate(
            content,
            allowedFindingIds: new HashSet<Guid> { findingId },
            allowedEvidenceIds: new HashSet<Guid> { evidenceId },
            systemRequiresHumanReview: false);

        Assert.True(outcome.IsValid);
        Assert.NotNull(outcome.SanitizedContent);
        Assert.Single(outcome.SanitizedContent.Explanations);
        Assert.False(outcome.FinalRequiresHumanReview);
    }

    [Fact]
    public void Validate_WithForgedFindingId_FiltersExplanationAndRecordsLimitation()
    {
        var forgedFindingId = Guid.NewGuid();
        var legitEvidenceId = Guid.NewGuid();

        var content = new AnalyticContent
        {
            Summary = "Analise com ID forjado.",
            Explanations =
            [
                new AnalyticContentExplanationsItem
                {
                    FindingIds = [forgedFindingId.ToString()],
                    EvidenceIds = [legitEvidenceId.ToString()],
                    Text = "Achado inventado pelo modelo."
                }
            ],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = false
        };

        var validator = new AnalyticResponseValidator();
        var outcome = validator.Validate(
            content,
            allowedFindingIds: new HashSet<Guid>(), // Vazio, forjado nao pertence
            allowedEvidenceIds: new HashSet<Guid> { legitEvidenceId },
            systemRequiresHumanReview: false);

        Assert.True(outcome.IsValid);
        Assert.Empty(outcome.SanitizedContent!.Explanations); // Filtrado
        Assert.Contains("UNKNOWN_FINDING_REFERENCED", outcome.LimitationCodes);
    }

    [Fact]
    public void Validate_SystemRequiresHumanReview_CannotBeOverriddenByLlm()
    {
        var content = new AnalyticContent
        {
            Summary = "Modelo tenta suprimir revisao humana.",
            Explanations = [],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = false // Modelo tenta desmarcar
        };

        var validator = new AnalyticResponseValidator();
        var outcome = validator.Validate(
            content,
            allowedFindingIds: new HashSet<Guid>(),
            allowedEvidenceIds: new HashSet<Guid>(),
            systemRequiresHumanReview: true); // Sistema exige revisao

        Assert.True(outcome.FinalRequiresHumanReview);
        Assert.True(outcome.SanitizedContent!.RequiresHumanReview);
    }
}
