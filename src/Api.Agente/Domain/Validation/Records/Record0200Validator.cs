using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0200Validator : IRecordValidator
{
    public string RecordCode => "0200";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 8 && record.Fields[0] == "0200";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0200.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // COD_MCAPT check (field 2)
        var isCodMcaptValid = !string.IsNullOrWhiteSpace(record.Fields[1]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0200.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isCodMcaptValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 9 && record.Fields.Length >= 8)
        {
            // CNPJ_TERM check (field 8, optional/conditional)
            var cnpj = record.Fields[7];
            var isCnpjValid = string.IsNullOrWhiteSpace(cnpj) || FieldValueRules.ValidateCnpj(cnpj);
            evaluations.Add(new RuleEvaluation(
                executionId,
                "DIMP10.0200.F08.TABLE",
                $"{lineKey}/field:8",
                RuleApplicability.Applicable,
                isCnpjValid ? RuleResult.Passed : RuleResult.Failed));
        }

        return evaluations;
    }
}
