using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1220Validator : IRecordValidator
{
    public string RecordCode => "1220";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 7 && record.Fields[0] == "1220";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1220.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        // UF_DEST check (field 2)
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1220.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
