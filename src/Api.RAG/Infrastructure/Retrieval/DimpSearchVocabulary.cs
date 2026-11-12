using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Api.RAG.Infrastructure.Retrieval;

internal static class DimpSearchVocabulary
{
    internal sealed record Term(string QuestionPattern, string[] EvidencePatterns, string SearchContext, string[] RecordCodes);

    // The evidence expressions match field definitions or validation text in the Manual V10.
    private static readonly Term[] Terms =
    [
        new(@"\bdt_op\b|\bdata\s+(?:da|de)\s+(?:venda|operacao)\b|\bresumo\s+diario\b",
            [@"\m03[[:space:]]+DT_OP\M"],
            "Registro 1110 campo 03 DT_OP Data da operação", ["1110"]),
        new(@"\b(?:comprovante|transacao|pagamento)\b.*\bresumo\s+diario\b|\bresumo\s+diario\b.*\b(?:comprovante|transacao|pagamento)\b",
            [@"\msoma dos valores do registro\M", @"\m1115[[:space:]]+deve ser igual ao valor informado no campo 04 do registro 1110\M"],
            "Registro 1115 Validação: a soma dos valores do registro 1115 deve ser igual ao valor informado no campo 04 do Registro 1110", ["1110", "1115"]),
        new(@"\b(?:hora|horario)\b.*\b(?:transacao|comprovante|pagamento|operacao)\b|\b(?:transacao|comprovante|pagamento|operacao)\b.*\b(?:hora|horario)\b",
            [@"\m07[[:space:]]+HORA\M"],
            "Registro 1115 campo 07 HORA Hora da transação", ["1115"]),
        new(@"\bdt_canc\b|\bdata\b.*\bcancel\w*\b|\bcancel\w*\b.*\bdata\b",
            [@"\m09[[:space:]]+DT_CANC\M"],
            "Registro 1200 campo 09 DT_CANC Data do cancelamento", ["1200"]),
        new(@"\bqtd_lin\b|\b(?:total|quantidade|numero)\b.*\blinhas\b.*\barquivo\b",
            [@"\mQTD_LIN\M"],
            "Registro 9999 campo 02 QTD_LIN Quantidade total de linhas do arquivo", ["9999"]),
        new(@"\bcodifica(?:cao|do|da)\b.*\b(?:caracteres?|arquivo|texto)\b|\b(?:caracteres?|arquivo|texto)\b.*\bcodifica(?:cao|do|da)\b|\b(?:ascii|latin-1|iso\s*8859-1)\b",
            [@"\mcodificado em ASCII[[:space:]]*-[[:space:]]*ISO 8859-1\M"],
            "Especificações técnicas do arquivo: formato texto, ASCII - ISO 8859-1 (Latin-1)", []),
        new(@"\b(?:separa\w*|delimita\w*|pipe)\b.*\bcampos?\b|\bcampos?\b.*\b(?:separa\w*|delimita\w*|pipe)\b",
            [@"\mPipe ou Barra Vertical\M"],
            "Especificações técnicas do arquivo: caractere delimitador Pipe ou Barra Vertical no início do registro e ao final de cada campo", []),
        new(@"\bremessa (?:de arquivo )?normal\b|\barquivo de remessa normal\b",
            [@"\mRemessa de arquivo normal\M"],
            "Registro 0000 campo 03 COD_FIN finalidade 1 Remessa de arquivo normal", ["0000"]),
        new(@"\bquem\b.*\b(?:declar\w*|inform\w*|presta\w*|report\w*)\b.*\b(?:pagamentos?|intermediac\w*)\b",
            [],
            "Informações gerais da DIMP: instituições financeiras e de pagamento e intermediadores de serviços e de negócios prestam informações mensalmente por unidade da Federação", []),
        new(@"\b(?:programa|aplicativo|sistema)\b.*\btransmi\w*\b|\btransmi\w*\b.*\b(?:programa|aplicativo|sistema)\b|\bted-tef\b",
            [@"\mtransmissão via TED-TEF\M"],
            "Informações gerais da DIMP: transmissão via TED-TEF, programa que valida, gera, assina digitalmente e transmite o arquivo", [])
    ];

    internal static Term[] Match(string question, IReadOnlyList<string>? recordCodes)
    {
        var normalized = string.Concat(question.ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
        normalized = Regex.Replace(normalized, @"\s+", " ");

        var mentionedRecords = Regex.Matches(normalized, @"\bregistros?\s+([0-9]{4})\b")
            .Select(match => match.Groups[1].Value).ToArray();

        return Terms.Where(term =>
            (term.RecordCodes.Length == 0 || recordCodes is null || recordCodes.Count == 0 || term.RecordCodes.Intersect(recordCodes).Any()) &&
            (term.RecordCodes.Length == 0 || mentionedRecords.Length == 0 || term.RecordCodes.Intersect(mentionedRecords).Any()) &&
            Regex.IsMatch(normalized, term.QuestionPattern, RegexOptions.NonBacktracking)).ToArray();
    }
}
