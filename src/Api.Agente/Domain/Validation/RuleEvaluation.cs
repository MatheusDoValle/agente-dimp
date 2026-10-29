namespace Api.Agente.Domain.Validation;

public enum RuleResult { Passed, Failed, NotApplicable, NotExecuted, BlockedBySourceConflict }
public enum RuleApplicability { Applicable, NotApplicable, Undetermined }

public sealed class RuleEvaluation
{
    private RuleEvaluation() { }
    public RuleEvaluation(Guid executionId, string ruleId, string occurrenceKey, RuleApplicability applicability, RuleResult result)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceKey);
        ValidateMetadata(ruleId, occurrenceKey);
        if (!Enum.IsDefined(applicability) || !Enum.IsDefined(result)
            || (applicability == RuleApplicability.NotApplicable) != (result == RuleResult.NotApplicable)
            || (applicability == RuleApplicability.Undetermined && result is not (RuleResult.NotExecuted or RuleResult.BlockedBySourceConflict)))
            throw new ArgumentException("Aplicabilidade e resultado incompativeis.");
        Id = Guid.NewGuid(); ExecutionId = executionId; RuleId = ruleId; OccurrenceKey = occurrenceKey;
        Applicability = applicability; Result = result;
    }
    public void ValidateMetadata() => ValidateMetadata(RuleId, OccurrenceKey);
    private static void ValidateMetadata(string ruleId, string occurrenceKey)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(ruleId, "^DIMP10[A-Za-z0-9._:-]{0,121}$")
            || !System.Text.RegularExpressions.Regex.IsMatch(occurrenceKey,
                "^(?:file|line:[1-9][0-9]*(?:/field:[1-9][0-9]*)?|context:[a-fA-F0-9-]{36})$"))
            throw new ArgumentException("Regra e ocorrencia exigem IDs tecnicos; nao usar identificadores do arquivo.");
    }

    public Guid Id { get; private set; }
    public Guid ExecutionId { get; private set; }
    public string RuleId { get; private set; } = "";
    public string OccurrenceKey { get; private set; } = "";
    public RuleApplicability Applicability { get; private set; }
    public RuleResult Result { get; private set; }
}

public sealed record CoverageTotals(long Applicable, long Passed, long Failed, long NotExecuted, long BlockedBySourceConflict)
{
    public static CoverageTotals FromEvaluations(IEnumerable<RuleEvaluation> evaluations)
    {
        var rows = evaluations.ToArray();
        if (rows.Select(x => x.ExecutionId).Distinct().Count() > 1) throw new ArgumentException("Cobertura mistura execucoes.");
        if (rows.GroupBy(x => (x.RuleId, x.OccurrenceKey)).Any(g => g.Count() > 1))
            throw new ArgumentException("Instancia de regra duplicada.");
        return new CoverageTotals(rows.LongCount(x => x.Result != RuleResult.NotApplicable),
            rows.LongCount(x => x.Result == RuleResult.Passed), rows.LongCount(x => x.Result == RuleResult.Failed),
            rows.LongCount(x => x.Result == RuleResult.NotExecuted), rows.LongCount(x => x.Result == RuleResult.BlockedBySourceConflict));
    }
}
