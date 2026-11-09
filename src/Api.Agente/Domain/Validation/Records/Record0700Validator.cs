namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0700Validator : IRecordValidator
{
    public string RecordCode => "0700";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 2 && record.Fields[0] == "0700";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0700.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        var isProcValid = !string.IsNullOrWhiteSpace(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0700.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isProcValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
