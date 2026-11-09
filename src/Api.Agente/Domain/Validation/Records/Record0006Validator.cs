using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0006Validator : IRecordValidator
{
    public string RecordCode => "0006";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 4 && record.Fields[0] == "0006";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0006.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CPF_RESP check
        var isCpfValid = FieldValueRules.ValidateCpf(record.Fields[2]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0006.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isCpfValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
