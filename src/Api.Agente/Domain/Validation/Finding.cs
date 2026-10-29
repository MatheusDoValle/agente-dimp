using Api.Agente.Domain.Privacy;

namespace Api.Agente.Domain.Validation;

public enum FindingCategory { StructuralError, BusinessInconsistency, AiIndication, MissingEvidence, TechnicalFailure }
public enum FindingSeverity { Blocking, Error, Warning, Information }
public enum FindingOrigin { Deterministic, Ai, System }

public sealed class Finding : IMaskedPayload
{
    private Finding() { }
    public Finding(Guid executionId, string? ruleId, FindingCategory category, FindingSeverity severity,
        FindingOrigin origin, MaskedText description, DocumentLocation location, IEnumerable<Guid> evidenceIds)
    {
        if (executionId == Guid.Empty || description.ExecutionId != executionId) throw new ArgumentException("Conteudo de outra execucao.");
        if (!Enum.IsDefined(category) || !Enum.IsDefined(severity) || !Enum.IsDefined(origin)) throw new ArgumentException("Classificacao invalida.");
        if (category == FindingCategory.AiIndication && origin != FindingOrigin.Ai) throw new ArgumentException("Indicio exige origem IA.");
        if (origin == FindingOrigin.Ai && category != FindingCategory.AiIndication) throw new ArgumentException("IA nao produz erro comprovado.");
        var evidence = evidenceIds.Distinct().ToArray();
        if (category is FindingCategory.StructuralError or FindingCategory.BusinessInconsistency
            && (string.IsNullOrWhiteSpace(ruleId) || evidence.Length == 0 || origin != FindingOrigin.Deterministic))
            throw new ArgumentException("Violacao comprovada exige regra, evidencia e validador.");
        Id = Guid.NewGuid(); ExecutionId = executionId; RuleId = ruleId; Category = category; Severity = severity;
        Origin = origin; Description = description.Value; Location = location; EvidenceIds = evidence;
    }
    public Guid Id { get; private set; }
    public Guid ExecutionId { get; private set; }
    public string? RuleId { get; private set; }
    public FindingCategory Category { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public FindingOrigin Origin { get; private set; }
    public string Description { get; private set; } = "";
    public DocumentLocation Location { get; private set; } = null!;
    public Guid[] EvidenceIds { get; private set; } = [];
    public IReadOnlyDictionary<string, string> GetMaskedFields() => new Dictionary<string, string> { [nameof(Description)] = Description };
}
