using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0209Validator : IRecordValidator
{
    public string RecordCode => "0209";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 10 && record.Fields[0] == "0209";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0209.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CNPJ check
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0209.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 7) return evaluations;

        // UF check
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[6]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0209.F07.TABLE",
            $"{lineKey}/field:7",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
