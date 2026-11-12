using System.Text.RegularExpressions;
using Api.Agente.Application.Privacy;
using Api.Agente.Infrastructure.Rag;

namespace Api.Agente.Application.Sofia;

internal sealed class SofiaManualQuestionHandler(
    SofiaDimpQuestions questions,
    IRagMcpClient rag,
    IConfiguration configuration,
    ILogger<SofiaManualQuestionHandler> logger)
{
    private const string GeneralObservation = "Consulta geral. Nenhum arquivo foi analisado.";
    private const int MaxQueryLength = 1200;
    private const int MaxCitations = 3;
    private const int ExcerptLimit = 480;

    internal async Task<ResultadoAnaliseSofia> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        var executionId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        ResultadoAnaliseSofia Result(string answer, string? limitation = null) =>
            ResultadoAnaliseSofia.CriarSucesso(executionId, requestId, answer, [],
                limitation is null ? [GeneralObservation] : [GeneralObservation, limitation]);

        var enabled = configuration.GetValue<bool>("ClassroomDemo:ManualSearchEnabled");
        if (enabled && question.Length > MaxQueryLength)
            return Result($"Resuma a dúvida sobre o Manual DIMP em até {MaxQueryLength} caracteres.",
                "QUERY_TOO_LONG");

        var query = enabled ? ContentSanitizer.MinimizeManualQuery(question) : question;
        if (questions.AnswerWithoutFile(query, enabled) is { } localAnswer)
            return Result(localAnswer);

        var manual = questions.Manual;
        var profile = configuration["Services:RagApi:EmbeddingProfileId"] ?? "text-embedding-3-small-1536";
        if (manual is null || string.IsNullOrWhiteSpace(profile))
            return Unavailable();

        var result = await rag.SearchManualAsync(query, manual.LayoutVersion, manual.RevisionSha256,
            profile, cancellationToken: cancellationToken);

        var noEvidence = result.LimitationCodes.Contains("NO_EVIDENCE_FOUND");
        if (result.LimitationCodes.Any(code => code != "NO_EVIDENCE_FOUND")
            || noEvidence != (result.Hits.Count == 0))
        {
            logger.LogWarning("Consulta ao Manual DIMP indisponível. IdExecucao={ExecutionId}; RetrievalId={RetrievalId}",
                executionId, result.RetrievalId);
            return Unavailable();
        }

        if (noEvidence)
        {
            return Result("Não encontrei evidência suficiente no Manual DIMP V10 para responder à pergunta. Se for uma dúvida sobre o leiaute, tente informar o registro, campo ou situação.",
                "NO_EVIDENCE_FOUND");
        }

        var citations = result.Hits
            .Where(hit => hit.Pages.Length > 0 && hit.Pages.All(page => page >= 1 && page <= manual.PageCount))
            .DistinctBy(hit => hit.ChunkId)
            .OrderByDescending(hit => hit.MatchType == "lexical")
            .Take(MaxCitations)
            .OrderBy(hit => hit.Pages.Min())
            .Select(hit => Cite(hit, manual))
            .ToArray();
        if (citations.Length == 0)
            return Unavailable();

        return Result($"Trechos do Manual DIMP V10 que respondem à pergunta:\n\n{string.Join("\n\n", citations)}\n\nRevisão S01 (SHA-256) {manual.RevisionSha256}.");

        ResultadoAnaliseSofia Unavailable() =>
            Result("A consulta ao Manual DIMP V10 está indisponível no momento. Tente novamente ou pergunte diretamente por um campo e registro do catálogo.",
                "MCP_UNAVAILABLE") with { Status = ResultadoAnaliseSofia.StatusParcial };
    }

    private static string Cite(McpHit hit, SofiaDimpQuestions.ManualIdentity manual)
    {
        var excerpt = Regex.Replace(hit.Text, @"\s+", " ").Trim();
        if (excerpt.Length > ExcerptLimit)
        {
            var head = excerpt.LastIndexOf(' ', ExcerptLimit / 2);
            var tail = excerpt.IndexOf(' ', excerpt.Length - ExcerptLimit / 2);
            excerpt = $"{excerpt[..(head > 0 ? head : ExcerptLimit / 2)]} […] {excerpt[(tail >= 0 ? tail + 1 : excerpt.Length - ExcerptLimit / 2)..]}";
        }

        var pages = hit.Pages.Distinct().Order().ToArray();
        var links = string.Join(", ", pages.Select(page => $"{manual.Url}#page={page}"));
        return $"“{excerpt}” [Manual DIMP V10, p. {string.Join(", ", pages)}; trecho {hit.ChunkId}; {links}]";
    }
}
