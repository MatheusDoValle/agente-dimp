using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1200Validator : IRecordValidator
{
    public string RecordCode => "1200";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 15 && record.Fields[0] == "1200";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1200.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 9) return evaluations;

        // DT_OP check (field 8)
        var isDtOpValid = FieldValueRules.ValidateDate(record.Fields[7], out var dtOp);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1200.F08.TABLE",
            $"{lineKey}/field:8",
            RuleApplicability.Applicable,
            isDtOpValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 10) return evaluations;

        // DT_CANC check (field 9: must be valid date and >= DT_OP)
        var isDtCancValid = FieldValueRules.ValidateDate(record.Fields[8], out var dtCanc) && (!isDtOpValid || dtCanc >= dtOp);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1200.F09.TABLE",
            $"{lineKey}/field:9",
            RuleApplicability.Applicable,
            isDtCancValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 12) return evaluations;

        // VL_CANC check (field 12)
        var isVlCancValid = FieldValueRules.ValidateDecimal(record.Fields[11], 2, out var vc) && vc >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1200.F12.TABLE",
            $"{lineKey}/field:12",
            RuleApplicability.Applicable,
            isVlCancValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
