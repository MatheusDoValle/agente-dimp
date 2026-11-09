using Api.Agente.Application.Contracts;

namespace Api.Agente.Application.AI;

public sealed record SemanticValidationOutcome(
    bool IsValid,
    AnalyticContent? SanitizedContent,
    IReadOnlyList<string> LimitationCodes,
    bool FinalRequiresHumanReview);

public sealed class AnalyticResponseValidator
{
    public SemanticValidationOutcome Validate(
        AnalyticContent content,
        IReadOnlySet<Guid> allowedFindingIds,
        IReadOnlySet<Guid> allowedEvidenceIds,
        bool systemRequiresHumanReview)
    {
        ArgumentNullException.ThrowIfNull(content);

        var limitations = new List<string>(content.Limitations);
        var validExplanations = new List<AnalyticContentExplanationsItem>();
        var validIndications = new List<AnalyticContentIndicationsItem>();

        // Valida explicacoes
        foreach (var exp in content.Explanations)
        {
            var validFindingIds = new List<string>();
            foreach (var fIdStr in exp.FindingIds)
            {
                if (Guid.TryParse(fIdStr, out var fGuid) && allowedFindingIds.Contains(fGuid))
                {
                    validFindingIds.Add(fIdStr);
                }
                else
                {
                    limitations.Add("UNKNOWN_FINDING_REFERENCED");
                }
            }

            var validEvidenceIds = new List<string>();
            foreach (var eIdStr in exp.EvidenceIds)
            {
                if (Guid.TryParse(eIdStr, out var eGuid) && allowedEvidenceIds.Contains(eGuid))
                {
                    validEvidenceIds.Add(eIdStr);
                }
                else
                {
                    limitations.Add("UNKNOWN_EVIDENCE_REFERENCED");
                }
            }

            if (validFindingIds.Count > 0)
            {
                validExplanations.Add(new AnalyticContentExplanationsItem
                {
                    FindingIds = validFindingIds,
                    EvidenceIds = validEvidenceIds,
                    Text = exp.Text
                });
            }
        }

        // Valida indicacoes
        foreach (var ind in content.Indications)
        {
            var validEvidenceIds = new List<string>();
            foreach (var eIdStr in ind.EvidenceIds)
            {
                if (Guid.TryParse(eIdStr, out var eGuid) && allowedEvidenceIds.Contains(eGuid))
                {
                    validEvidenceIds.Add(eIdStr);
                }
                else
                {
                    limitations.Add("UNKNOWN_EVIDENCE_REFERENCED");
                }
            }

            validIndications.Add(new AnalyticContentIndicationsItem
            {
                EvidenceIds = validEvidenceIds,
                Text = ind.Text
            });
        }

        // O sistema mantem autoridade sobre revisao humana: OR logico
        var finalRequiresHumanReview = systemRequiresHumanReview || content.RequiresHumanReview;

        var sanitizedContent = new AnalyticContent
        {
            Summary = content.Summary,
            Explanations = validExplanations,
            Indications = validIndications,
            Limitations = limitations.Distinct().ToList(),
            RequiresHumanReview = finalRequiresHumanReview
        };

        return new SemanticValidationOutcome(
            IsValid: true,
            SanitizedContent: sanitizedContent,
            LimitationCodes: limitations.Distinct().ToList(),
            FinalRequiresHumanReview: finalRequiresHumanReview);
    }
}
