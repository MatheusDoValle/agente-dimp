namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0005Validator : IRecordValidator
{
    internal const int PostalCodeIndex = 3;
    internal const int FieldCount = 9;

    public string RecordCode => "0005";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        var isStructureValid = record.Fields.Length == FieldCount && record.Fields[0] == "0005";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0005.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // END check (field 3: required text)
        var end = record.Fields[2];
        var isEndValid = !string.IsNullOrWhiteSpace(end);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0005.F03.TABLE",
            $"{lineKey}/field:3",
            RuleApplicability.Applicable,
            isEndValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length <= PostalCodeIndex) return evaluations;

        var cep = record.Fields[PostalCodeIndex];
        var isCepValid = string.IsNullOrWhiteSpace(cep) || IsPostalCodeFormat(cep);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0005.F04.TABLE",
            $"{lineKey}/field:4",
            RuleApplicability.Applicable,
            isCepValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }

    internal static bool IsPostalCodeFormat(string value) => value.Length == 8 && value.All(char.IsAsciiDigit);
}
