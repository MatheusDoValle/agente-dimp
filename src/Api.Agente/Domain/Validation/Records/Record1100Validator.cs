using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record1100Validator : IRecordValidator
{
    public string RecordCode => "1100";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 9 && record.Fields[0] == "1100";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1100.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // COD_CLIENTE check (field 3)
        var isCliValid = !string.IsNullOrWhiteSpace(record.Fields[2]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1100.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isCliValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 7) return evaluations;

        // DT_INI and DT_FIN check
        var isDtIniValid = FieldValueRules.ValidateDate(record.Fields[5], out var dtIni);
        var isDtFinValid = FieldValueRules.ValidateDate(record.Fields[6], out var dtFin) && (!isDtIniValid || dtFin >= dtIni);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1100.F06.TABLE",
            $"{lineKey}/field:6",
            RuleApplicability.Applicable,
            isDtIniValid ? RuleResult.Passed : RuleResult.Failed));
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1100.F07.TABLE",
            $"{lineKey}/field:7",
            RuleApplicability.Applicable,
            isDtFinValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 8) return evaluations;

        // VALOR check
        var isValorValid = FieldValueRules.ValidateDecimal(record.Fields[7], 2, out var v) && v >= 0m;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.1100.F08.TABLE",
            $"{lineKey}/field:8",
            RuleApplicability.Applicable,
            isValorValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
