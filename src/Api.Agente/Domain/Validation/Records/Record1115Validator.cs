using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1115Validator : IRecordValidator
{
    public string RecordCode => "1115";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 12 && record.Fields[0] == "1115";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1115.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 7) return evaluations;

        // HORA check (field 7)
        var isHoraValid = FieldValueRules.ValidateTime(record.Fields[6], out _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1115.F07.TABLE",
            $"{lineKey}/field:7",
            RuleApplicability.Applicable,
            isHoraValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 8) return evaluations;

        // VALOR check (field 8)
        var isValorValid = FieldValueRules.ValidateDecimal(record.Fields[7], 2, out var v) && v >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1115.F08.TABLE",
            $"{lineKey}/field:8",
            RuleApplicability.Applicable,
            isValorValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length >= 12)
        {
            // IND_TP_PIX check (field 12) -> Blocked by SF-003 conflict
            evaluations.Add(new RuleEvaluation(
                executionId,
                "DIMP10.1115.F12.TABLE",
                $"{lineKey}/field:12",
                RuleApplicability.Undetermined,
                RuleResult.BlockedBySourceConflict));
        }

        return evaluations;
    }
}
