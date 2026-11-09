namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1001Validator : IRecordValidator
{
    public string RecordCode => "1001";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 2 && record.Fields[0] == "1001";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1001.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        var indMov = record.Fields[1];
        var isIndMovValid = indMov is "0" or "1";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1001.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isIndMovValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
