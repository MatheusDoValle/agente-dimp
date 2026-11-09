using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0990Validator : IRecordValidator
{
    public string RecordCode => "0990";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 2 && record.Fields[0] == "0990";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0990.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        // QTD_LIN_0 check (must be a valid integer > 0)
        var isQtdValid = FieldValueRules.ValidateInteger(record.Fields[1], 1, 12, out var qtd) && qtd > 0;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0990.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isQtdValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
