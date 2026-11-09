using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1600Validator : IRecordValidator
{
    public string RecordCode => "1600";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 10 && record.Fields[0] == "1600";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1600.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 4) return evaluations;

        // PERIODO check (field 4)
        var isPeriodoValid = FieldValueRules.ValidateYearMonth(record.Fields[3], out _, out _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1600.F04.TABLE",
            $"{lineKey}/field:4",
            RuleApplicability.Applicable,
            isPeriodoValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 5) return evaluations;

        // VL_CANC check (field 5)
        var isVlCancValid = FieldValueRules.ValidateDecimal(record.Fields[4], 2, out var v) && v >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1600.F05.TABLE",
            $"{lineKey}/field:5",
            RuleApplicability.Applicable,
            isVlCancValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
