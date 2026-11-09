namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0001Validator : IRecordValidator
{
    public string RecordCode => "0001";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 2 && record.Fields[0] == "0001";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0001.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        var indDad = record.Fields[1];
        var isIndDadValid = indDad is "0" or "1";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0001.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isIndDadValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
