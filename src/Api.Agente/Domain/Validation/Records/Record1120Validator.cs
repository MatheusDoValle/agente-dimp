using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1120Validator : IRecordValidator
{
    public string RecordCode => "1120";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 14 && record.Fields[0] == "1120";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1120.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        // UF_DEST check (field 2)
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1120.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length >= 14)
        {
            // COD_MUN_DEST check (field 14) -> Blocked by SF-002 conflict (7 vs 8 digits)
            evaluations.Add(new RuleEvaluation(
                executionId,
                "DIMP10.1120.F14.TABLE",
                $"{lineKey}/field:14",
                RuleApplicability.Undetermined,
                RuleResult.BlockedBySourceConflict));
        }

        return evaluations;
    }
}
