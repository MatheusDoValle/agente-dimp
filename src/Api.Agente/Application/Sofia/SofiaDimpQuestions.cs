using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Agente.Domain.Validation;

namespace Api.Agente.Application.Sofia;

public sealed class SofiaDimpQuestions(RuleCatalog catalog)
{
    internal const int MaxDetails = 5;
    internal const string GeneralHelp = "Posso responder dúvidas gerais sobre a DIMP, orientar por onde começar ou explicar um registro do Manual V10. Experimente perguntar “O que é DIMP?” ou “O que significa o campo 2 do registro 9999?”.";
    internal sealed record ManualIdentity(string LayoutVersion, string RevisionSha256, Uri Url, int PageCount);
    internal ManualIdentity? Manual { get; private init; }
    private const RegexOptions IgnoreCase = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex RuleId = new(@"\bDIMP10\.[A-Z0-9_:-]+(?:\.[A-Z0-9_:-]+)+", IgnoreCase);
    private static readonly Regex RecordNumber = new(@"\b[0-9]{4}\b");
    private static readonly Regex LineNumber = new(@"\blinhas?\s+([0-9]+)\b", IgnoreCase);
    private static readonly Regex FieldNumber = new(@"\bcampos?\s+([0-9]+)\b", IgnoreCase);
    private static readonly Regex AboutFindings = new(@"\b(?:achados?|erros?|inconsist[êe]ncias?|corrig\w*|corre[çc][ãa]o|problemas?|falhas?|resum\w*)\b", IgnoreCase);
    private static readonly Regex DefinitionQuestion = new(@"\b(?:o (?:que|q) [eé]|o que significa|qual o significado de|(?:pra|para) que serve)\s+(?:a\s+)?dimp\b", IgnoreCase);
    private static readonly Regex HelpQuestion = new(@"\b(?:ajudar|ajuda|o que (?:voc[eê]|vc) (?:faz|consegue fazer))\b", IgnoreCase);
    private static readonly Regex NextStepQuestion = new(@"\b(?:o (?:que|q) (?:eu )?(?:devo|preciso) fazer|como (?:eu )?(?:come[cç]o|procedo|preparo|fa[cç]o))\b", IgnoreCase);
    private static readonly Regex GeneralHelpQuestion = new(
        @"^\s*(?:(?:(?:como|em que|no que)\s+)?(?:(?:voc[eê]|vc)\s+)?(?:pode|consegue)\s+(?:me\s+)?ajudar|o que (?:voc[eê]|vc) (?:faz|consegue fazer)|ajuda)[\s?!\.]*$",
        IgnoreCase);
    private static readonly Regex GeneralNextStepQuestion = new(
        @"^\s*(?:o (?:que|q) (?:eu )?(?:devo|preciso) fazer|como (?:eu )?(?:come[cç]o|procedo|preparo|fa[cç]o))(?:\s+(?:agora|com (?:a )?dimp|(?:a |o arquivo )?dimp))?[\s?!\.]*$",
        IgnoreCase);
    private static readonly Regex FileReviewQuestion = new(
        @"\b(?:meu arquivo|minha dimp|arquivo anexado|dimp anexad[ao]|preciso corrigir|devo corrigir|o que corrigir)\b", IgnoreCase);
    private static readonly Regex OperationDateQuestion = new(@"\bdata\s+d[ae]\s+(?:venda|transa[çc][ãa]o|opera[çc][ãa]o)\b", IgnoreCase);
    private static readonly Regex CancelledOperation = new(@"\b(?:cancelament\w*|cancelad\w*|origin(?:al|ais))\b", IgnoreCase);

    internal static SofiaDimpQuestions Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "dimp-v10");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(path, "manifest.json")));
        var expectedHash = manifest.RootElement.GetProperty("catalogSha256").GetString()
            ?? throw new InvalidDataException("Hash do catálogo DIMP ausente.");
        var bytes = File.ReadAllBytes(Path.Combine(path, "catalog.json"));
        if (Convert.ToHexString(SHA256.HashData(bytes)) != expectedHash)
        {
            // Git can check out LF even though this catalog was pinned with CRLF.
            var text = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal);
            bytes = Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal));
        }
        var catalog = RuleCatalog.Load(bytes, expectedHash);
        var source = manifest.RootElement.GetProperty("source");
        var revision = source.GetProperty("revisionSha256").GetString();
        var layout = manifest.RootElement.GetProperty("layoutVersion").GetString();
        if (layout != "10" || !string.Equals(revision, catalog.RevisionSha256, StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(source.GetProperty("url").GetString(), UriKind.Absolute, out var url)
            || url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("Identidade do Manual DIMP inválida.");
        }

        return new SofiaDimpQuestions(catalog)
        {
            Manual = new(layout, catalog.RevisionSha256.ToLowerInvariant(), url, source.GetProperty("pageCount").GetInt32())
        };
    }

    internal sealed record FindingScope(string Label, Func<RuleEvaluation, bool> Matches);

    internal FindingScope? FindScope(string question)
    {
        if (RuleId.IsMatch(question)) return null;

        var lineMatch = LineNumber.Match(question);
        if (lineMatch.Success && int.TryParse(lineMatch.Groups[1].Value, out var line))
        {
            var key = $"line:{line}";
            return new($"na linha {line}", f => f.OccurrenceKey == key ||
                f.OccurrenceKey.StartsWith($"{key}/", StringComparison.Ordinal));
        }

        if (!AboutFindings.IsMatch(question)) return null;

        var record = FindRecord(question);
        return record is null ? new("no arquivo", _ => true)
            : new($"no registro {record}", f => f.RuleId.StartsWith($"DIMP10.{record}.", StringComparison.Ordinal));
    }

    internal string? Answer(string question, FindingScope? scope, IReadOnlyList<RuleEvaluation> samples, int count)
    {
        if (scope is not null) return Summarize(samples, count, scope.Label);

        return ExplainReference(question) ?? (question.StartsWith("Valide ", StringComparison.OrdinalIgnoreCase) ? null
            : "Posso explicar os achados do arquivo ou consultar uma regra do DIMP V10. Experimente perguntar o que corrigir, se há erro na linha 19 ou o que significa o campo 2 do registro 9999.");
    }

    internal string? AnswerWithoutFile(string question, bool allowManualSearch)
    {
        if (DefinitionQuestion.IsMatch(question))
            return "DIMP significa Declaração de Informações de Meios de Pagamentos. É um arquivo padronizado que reúne informações sobre transações de pagamento e operações intermediadas para os fiscos estaduais. O Manual DIMP V10 do CONFAZ descreve como os dados são organizados em blocos, registros e campos.";

        if ((allowManualSearch ? GeneralHelpQuestion : HelpQuestion).IsMatch(question))
            return "Posso explicar o que é DIMP e consultar registros, campos e regras no Manual DIMP V10. Se você anexar um arquivo, posso apontar inconsistências, indicar as linhas e explicar as regras relacionadas aos achados.";

        if ((allowManualSearch ? GeneralNextStepQuestion : NextStepQuestion).IsMatch(question))
            return "Se você está começando, identifique a UF, o período e a versão do leiaute do arquivo. Se já tem um DIMP gerado, anexe-o aqui para revisar a estrutura e os achados. Você quer entender a declaração, preparar um arquivo ou revisar um que já tem?";

        if (allowManualSearch
            ? LineNumber.IsMatch(question) || FileReviewQuestion.IsMatch(question)
            : FindScope(question) is not null)
            return "Para verificar erros ou sugerir correções no seu arquivo, anexe a DIMP. Para uma dúvida geral, indique o registro ou o campo do manual que você quer entender.";

        return ExplainReference(question) ?? (allowManualSearch ? null : GeneralHelp);
    }

    private string? ExplainReference(string question)
    {
        var id = RuleId.Match(question).Value;
        if (id.Length > 0)
        {
            var rule = catalog.Rules.FirstOrDefault(rule => string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase));
            return rule is null ? $"A regra {id} não consta no catálogo DIMP V10." : Explain(rule);
        }

        var record = FindRecord(question);
        if (record is not null)
        {
            var definitions = catalog.Rules.Where(r => r.Record == record);
            if (int.TryParse(FieldNumber.Match(question).Groups[1].Value, out var field))
                definitions = definitions.Where(r => r.Field == field);

            var notes = definitions.OrderBy(r => r.Field.HasValue).ThenBy(r => r.Field)
                .Take(MaxDetails).Select(Explain).ToArray();
            return notes.Length == 0 ? $"Não encontrei essa referência para o registro {record} no catálogo DIMP V10."
                : string.Join("\n", notes);
        }

        if (OperationDateQuestion.IsMatch(question) && !CancelledOperation.IsMatch(question) &&
            catalog.Rules.FirstOrDefault(rule => rule.Id == "DIMP10.1110.F03.TABLE") is { } operationDate)
        {
            return Explain(operationDate);
        }

        return null;
    }

    private static string? FindRecord(string question) => RecordNumber.Matches(question).Select(m => m.Value)
        .FirstOrDefault(code => RuleCatalog.RecordCodes.Contains(code));

    private string Summarize(IReadOnlyList<RuleEvaluation> samples, int count, string scope)
    {
        if (count == 0)
            return $"Nenhum achado {scope} entre as regras que foram avaliadas.";

        var details = samples.Select(f =>
        {
            if (DescribeFileFinding(f) is { } description) return $"- {description}";
            var rule = catalog.Rules.FirstOrDefault(r => r.Id == f.RuleId);
            return rule is null ? $"- {f.OccurrenceKey}: revise a regra {f.RuleId}."
                : $"- {f.OccurrenceKey}: {Explain(rule)}";
        });
        var remaining = count > samples.Count ? $"\nMais {count - samples.Count} achado(s) neste recorte." : "";
        return $"{count} achado(s) {scope}.\n{string.Join("\n", details)}{remaining}";
    }

    internal static string? DescribeFileFinding(RuleEvaluation finding)
    {
        var location = finding.OccurrenceKey.Replace("line:", "Linha ", StringComparison.Ordinal)
            .Replace("/field:", ", campo ", StringComparison.Ordinal);

        return finding.RuleId switch
        {
            "DIMP10.FILE.PIPE" when finding.PossiblePostalCodeRecordLine is long previousLine =>
                $"{location}. Erro de estrutura. Há um número de oito dígitos em uma linha isolada. " +
                $"O campo 04 (CEP) do registro 0005 na linha {previousLine} está vazio. " +
                $"Confira o CEP na origem e, se o número corresponder, coloque-o nesse campo da linha {previousLine}, " +
                "mantendo os oito dígitos. Depois, remova a linha isolada e confira os totalizadores após a correção.",
            "DIMP10.FILE.PIPE" =>
                $"{location}. Erro de estrutura. Falta o delimitador `|` no início ou no fim da linha. " +
                "Confira a linha no arquivo de origem e recomponha o registro completo, com os campos na ordem do leiaute.",
            "DIMP10.FILE.FIELD_NOTATION" =>
                $"{location}. Erro de estrutura. O código do registro não foi reconhecido. " +
                "Confira o código e o posicionamento dos delimitadores `|` com o leiaute DIMP V10.",
            "DIMP10.0005.STRUCTURE" =>
                $"{location}, registro 0005. Erro de estrutura. O registro não tem a quantidade esperada de campos. " +
                "Confira a ordem dos campos no Manual DIMP V10. Revise delimitadores extras e campos cortados " +
                "ou separados por uma quebra de linha.",
            "DIMP10.0005.F04.TABLE" =>
                $"{location}, registro 0005 (CEP). O CEP deve ter oito dígitos. " +
                "Confira o CEP na origem e informe os oito dígitos, sem hífen ou letras, preservando o zero inicial quando houver.",
            _ => null
        };
    }

    private static string Explain(RuleDefinition rule)
    {
        var limitation = rule.Interpretation == "resolved" ? "" : " O catálogo registra dúvida ou conflito nesta regra.";
        return $"{rule.Statement} [Manual DIMP V10, p. {rule.Source.Page}, {rule.Id}].{limitation}";
    }
}
