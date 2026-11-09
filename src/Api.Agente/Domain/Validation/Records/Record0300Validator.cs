using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0300Validator : IRecordValidator
{
    public string RecordCode => "0300";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 11 && record.Fields[0] == "0300";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0300.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CNPJ check (field 3)
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[2]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0300.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 8) return evaluations;

        // UF check (field 8)
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[7]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0300.F08.TABLE",
            $"{lineKey}/field:8",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
