using Api.Agente.Domain.Validation.Common;

namespace Api.Agente.Domain.Validation.Records;

public sealed class Record0000Validator : IRecordValidator
{
    public string RecordCode => "0000";

    public IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (executionId == Guid.Empty) throw new ArgumentException("Execucao obrigatoria.");

        var evaluations = new List<RuleEvaluation>();
        var lineKey = $"line:{record.LineNumber}";

        // Structure check: 10 fields and starts with 0000
        var isStructureValid = record.Fields.Length == 10 && record.Fields[0] == "0000";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.STRUCTURE",
            lineKey,
            RuleApplicability.Applicable,
            isStructureValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 2) return evaluations;

        // COD_VER check
        var isVersionValid = record.Fields[1] == "10";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.COD_VER.APPLICABILITY",
            $"{lineKey}/field:2",
            RuleApplicability.Applicable,
            isVersionValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 3) return evaluations;

        // COD_FIN check (SF-001 conflict: 9 is blocked_by_source_conflict)
        var codFin = record.Fields[2];
        RuleResult finResult;
        RuleApplicability finApp;
        if (codFin == "9")
        {
            finResult = RuleResult.BlockedBySourceConflict;
            finApp = RuleApplicability.Undetermined;
        }
        else if (codFin is "1" or "2" or "3" or "4" or "5" or "6")
        {
            finResult = RuleResult.Passed;
            finApp = RuleApplicability.Applicable;
        }
        else
        {
            finResult = RuleResult.Failed;
            finApp = RuleApplicability.Applicable;
        }

        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.COD_FIN.DOMAIN",
            $"{lineKey}/field:3",
            finApp,
            finResult));

        if (record.Fields.Length < 4) return evaluations;

        // UF check
        var isUfValid = FieldValueRules.ValidateUf(record.Fields[3]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F04.TABLE",
            $"{lineKey}/field:4",
            RuleApplicability.Applicable,
            isUfValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 5) return evaluations;

        // CNPJ check
        var isCnpjValid = FieldValueRules.ValidateCnpj(record.Fields[4]);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F05.TABLE",
            $"{lineKey}/field:5",
            RuleApplicability.Applicable,
            isCnpjValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 6) return evaluations;

        // NOME check
        var nome = record.Fields[5];
        var isNomeValid = !string.IsNullOrWhiteSpace(nome) && nome.Length <= 60;
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F06.TABLE",
            $"{lineKey}/field:6",
            RuleApplicability.Applicable,
            isNomeValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 7) return evaluations;

        // DT_INI check
        var isDtIniValid = FieldValueRules.ValidateDate(record.Fields[6], out var dtIni);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F07.TABLE",
            $"{lineKey}/field:7",
            RuleApplicability.Applicable,
            isDtIniValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 8) return evaluations;

        // DT_FIN check (must be valid date and >= DT_INI when DT_INI is valid)
        var isDtFinValid = FieldValueRules.ValidateDate(record.Fields[7], out var dtFin) && (!isDtIniValid || dtFin >= dtIni);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F08.TABLE",
            $"{lineKey}/field:8",
            RuleApplicability.Applicable,
            isDtFinValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 9) return evaluations;

        // IND_SIT_ESP check
        var sitEsp = record.Fields[8];
        var isSitEspValid = sitEsp is "" or "0" or "1" or "2" or "3" or "4" or "9";
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F09.TABLE",
            $"{lineKey}/field:9",
            RuleApplicability.Applicable,
            isSitEspValid ? RuleResult.Passed : RuleResult.Failed));

        if (record.Fields.Length < 10) return evaluations;

        // MES_REF check
        var isMesRefValid = FieldValueRules.ValidateYearMonth(record.Fields[9], out var _, out var _);
        evaluations.Add(new RuleEvaluation(
            executionId,
            "DIMP10.0000.F10.TABLE",
            $"{lineKey}/field:10",
            RuleApplicability.Applicable,
            isMesRefValid ? RuleResult.Passed : RuleResult.Failed));

        return evaluations;
    }
}
