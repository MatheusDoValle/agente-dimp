using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0100Validator : IRecordValidator
{
    public string RecordCode => "0100";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 14 && record.Fields[0] == "0100";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0100.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CNPJ or CPF check (field 3 is CNPJ, field 4 is CPF)
        var cnpj = record.Fields[2];
        var cpf = record.Fields.Length > 3 ? record.Fields[3] : "";
        var hasValidCnpj = !string.IsNullOrWhiteSpace(cnpj) && FieldValueRules.ValidateCnpj(cnpj);
        var hasValidCpf = !string.IsNullOrWhiteSpace(cpf) && FieldValueRules.ValidateCpf(cpf);
        var isDocValid = hasValidCnpj || hasValidCpf;

        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0100.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isDocValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 9) return evaluations;

        // UF check (field 9)
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[8]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0100.F09.TABLE",
            $"{lineKey}/field:9",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 13) return evaluations;

        // DT_CREDEN check (field 13)
        var isDtCredenValid = FieldValueRules.ValidateDate(record.Fields[12], out _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0100.F13.TABLE",
            $"{lineKey}/field:13",
            RuleApplicability.Applicable,
            isDtCredenValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
