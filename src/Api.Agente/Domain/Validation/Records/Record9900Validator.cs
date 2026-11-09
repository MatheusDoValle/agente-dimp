using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record9900Validator : IRecordValidator
{
    public string RecordCode => "9900";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 3 && record.Fields[0] == "9900";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.9900.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // QTD_REG_BLC check
        var isQtdValid = FieldValueRules.ValidateInteger(record.Fields[2], 1, 12, out var qtd) && qtd > 0;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.9900.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isQtdValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
