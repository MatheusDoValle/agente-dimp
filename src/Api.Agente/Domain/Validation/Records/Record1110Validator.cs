using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1110Validator : IRecordValidator
{
    public string RecordCode => "1110";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 6 && record.Fields[0] == "1110";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1110.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // DT_OP check (field 3)
        var isDtOpValid = FieldValueRules.ValidateDate(record.Fields[2], out _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1110.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isDtOpValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 4) return evaluations;

        // VALOR check
        var isValorValid = FieldValueRules.ValidateDecimal(record.Fields[3], 2, out var v) && v >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1110.F04.TABLE",
            $"{lineKey}/field:4",
            RuleApplicability.Applicable,
            isValorValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 6) return evaluations;

        // CNPJ_LIQ check
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[5]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1110.F06.TABLE",
            $"{lineKey}/field:6",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
