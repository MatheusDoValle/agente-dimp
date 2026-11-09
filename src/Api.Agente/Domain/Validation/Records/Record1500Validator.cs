using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1500Validator : IRecordValidator
{
    public string RecordCode => "1500";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 12 && record.Fields[0] == "1500";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1500.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 6) return evaluations;

        // PERIODO check (field 6)
        var isPeriodoValid = FieldValueRules.ValidateYearMonth(record.Fields[5], out _, out _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1500.F06.TABLE",
            $"{lineKey}/field:6",
            RuleApplicability.Applicable,
            isPeriodoValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 7) return evaluations;

        // VALOR check (field 7)
        var isValorValid = FieldValueRules.ValidateDecimal(record.Fields[6], 2, out var v) && v >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1500.F07.TABLE",
            $"{lineKey}/field:7",
            RuleApplicability.Applicable,
            isValorValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
