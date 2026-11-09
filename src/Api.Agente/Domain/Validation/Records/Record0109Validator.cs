using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0109Validator : IRecordValidator
{
    public string RecordCode => "0109";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 4 && record.Fields[0] == "0109";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0109.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CNPJ_PROVEDOR check
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0109.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 4) return evaluations;

        // VALOR TOTAL check
        var isValorValid = FieldValueRules.ValidateDecimal(record.Fields[3], 2, out var v) && v >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0109.F04.TABLE",
            $"{lineKey}/field:4",
            RuleApplicability.Applicable,
            isValorValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
