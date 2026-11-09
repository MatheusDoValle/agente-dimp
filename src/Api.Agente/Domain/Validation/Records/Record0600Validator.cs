using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0600Validator : IRecordValidator
{
    public string RecordCode => "0600";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 14 && record.Fields[0] == "0600";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0600.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CNPJ check
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[2]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0600.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 9) return evaluations;

        // UF check
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[8]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0600.F09.TABLE",
            $"{lineKey}/field:9",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 13) return evaluations;

        // DT_INI_AUT check
        var isDtIniValid = FieldValueRules.ValidateDate(record.Fields[12], out _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0600.F13.TABLE",
            $"{lineKey}/field:13",
            RuleApplicability.Applicable,
            isDtIniValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
