namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0201Validator : IRecordValidator
{
    public string RecordCode => "0201";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 2 && record.Fields[0] == "0201";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0201.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        var isClienteValid = !string.IsNullOrWhiteSpace(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0201.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isClienteValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
