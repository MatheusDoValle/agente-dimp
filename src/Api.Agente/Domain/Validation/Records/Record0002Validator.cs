using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0002Validator : IRecordValidator
{
    public string RecordCode => "0002";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == 4 && record.Fields[0] == "0002";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0002.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        // ESTAB check (domain 0 or 1)
        var estab = record.Fields[1];
        var isEstabValid = estab is "0" or "1";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0002.F02.TABLE",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isEstabValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // CNPJ_ESTAB check
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[2]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0002.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 4) return evaluations;

        // NOME_ESTAB check
        var nome = record.Fields[3];
        var isNomeValid = !string.IsNullOrWhiteSpace(nome) && nome.Length <= 60;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0002.F04.TABLE",
            $"{lineKey}/field:4",
            RuleApplicability.Applicable,
            isNomeValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
