using System.Runtime.CompilerServices;
using System.Text;
using Api.Agente.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Application.Sofia;

public sealed class SofiaAnalysisHandler : ISofiaAnalysisHandler
{
    internal static readonly Encoding DimpEncoding =
        Encoding.GetEncoding("iso-8859-1", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    private readonly IDimpParser _parser;
    private readonly DimpValidationEngine _validationEngine;
    private readonly ILogger<SofiaAnalysisHandler> _logger;
    private readonly SofiaDimpQuestions? _questions;

    public SofiaAnalysisHandler(
        IDimpParser parser,
        DimpValidationEngine validationEngine,
        ILogger<SofiaAnalysisHandler> logger,
        SofiaDimpQuestions? questions = null)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _validationEngine = validationEngine ?? throw new ArgumentNullException(nameof(validationEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _questions = questions;
    }

    public async Task<ResultadoAnaliseSofia> HandleAsync(SolicitacaoAnaliseSofia solicitacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        solicitacao.Validate();

        _logger.LogInformation("Iniciando analise SOFIA para idExecucao: {IdExecucao}, idSolicitacao: {IdSolicitacao}",
            solicitacao.IdExecucao, solicitacao.IdSolicitacao);

        var dimpDoc = solicitacao.Documentos.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(d.Texto) &&
            (d.Texto.Contains("|0000|", StringComparison.Ordinal) ||
             d.Nome.EndsWith(".dimp", StringComparison.OrdinalIgnoreCase) ||
             (d.Texto.StartsWith('|') && d.Texto.Contains("\n|", StringComparison.Ordinal))));

        if (dimpDoc == null)
        {
            return ResultadoAnaliseSofia.CriarSucesso(
                solicitacao.IdExecucao,
                solicitacao.IdSolicitacao,
                $"Nenhum arquivo no formato fiscal DIMP (Ato COTEPE/ICMS 65/2018) foi identificado entre os {solicitacao.Documentos.Count} documento(s) recebido(s). O Agente Fiscal DIMP analisa registros delimitados por pipes (|0000|, |0100|, etc.).",
                achados: Array.Empty<AchadoSofia>(),
                observacoes: new[] { "Nenhum leiaute DIMP correspondente localizado para validacao estrutural." }
            );
        }

        try
        {
            var rawBytes = DimpEncoding.GetBytes(dimpDoc.Texto);
            using var stream = new MemoryStream(rawBytes);
            return await AnalyzeAsync(solicitacao, dimpDoc, stream, limitarDetalhes: false, cancellationToken);
        }
        catch (EncoderFallbackException)
        {
            return EncodingError(solicitacao);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha durante o processamento do arquivo DIMP para solicitacao {IdSolicitacao}", solicitacao.IdSolicitacao);
            return ResultadoAnaliseSofia.CriarErro(
                solicitacao.IdExecucao,
                solicitacao.IdSolicitacao,
                "ERRO_PROCESSAMENTO_DIMP",
                ex.Message,
                $"Ocorreu um erro ao processar o arquivo DIMP '{dimpDoc.Nome}'."
            );
        }
    }

    public async Task<ResultadoAnaliseSofia> HandleUploadAsync(
        SolicitacaoAnaliseSofia solicitacao, Stream arquivo, CancellationToken cancellationToken = default)
    {
        solicitacao.Validate();
        var document = solicitacao.Documentos.Single();
        try
        {
            return await AnalyzeAsync(solicitacao, document, arquivo, limitarDetalhes: true, cancellationToken);
        }
        catch (EncoderFallbackException)
        {
            return EncodingError(solicitacao);
        }
        catch (InvalidDataException ex)
        {
            return ResultadoAnaliseSofia.CriarErro(
                solicitacao.IdExecucao, solicitacao.IdSolicitacao, "ARQUIVO_DIMP_INVALIDO",
                ex.Message, "Não foi possível validar o arquivo enviado.");
        }
    }

    private async Task<ResultadoAnaliseSofia> AnalyzeAsync(
        SolicitacaoAnaliseSofia solicitacao, DocumentoSofia document, Stream stream,
        bool limitarDetalhes, CancellationToken cancellationToken)
    {
        const int maxDetails = 200;
        var seen = new HashSet<(string RuleId, string OccurrenceKey)>();
        var findings = new List<AchadoSofia>();
        var questionScope = _questions?.FindScope(solicitacao.Pergunta);
        var questionSamples = new List<RuleEvaluation>();
        var questionCount = 0;
        var evaluated = 0;
        var failures = 0;
        var records = limitarDetalhes ? ReadUploadRecords(cancellationToken) : _parser.ReadAsync(stream, cancellationToken);

        await foreach (var evaluation in _validationEngine.ValidateAsync(records, null, solicitacao.IdExecucao, cancellationToken)
                           .ConfigureAwait(false))
        {
            evaluated++;
            if (evaluation.Result != RuleResult.Failed || !seen.Add((evaluation.RuleId, evaluation.OccurrenceKey)))
                continue;

            failures++;
            if (questionScope?.Matches(evaluation) == true)
            {
                questionCount++;
                if (questionSamples.Count < SofiaDimpQuestions.MaxDetails) questionSamples.Add(evaluation);
            }

            if (!limitarDetalhes || findings.Count < maxDetails)
            {
                findings.Add(new AchadoSofia(
                    $"ach-{failures:D3}",
                    SofiaDimpQuestions.DescribeFileFinding(evaluation) ??
                    $"Inconsistencia fiscal na regra {evaluation.RuleId} ({evaluation.OccurrenceKey}).",
                    [document.IdDocumento],
                    [$"Falha detectada na regra {evaluation.RuleId} em {evaluation.OccurrenceKey} no documento {document.Nome}."],
                    DeterminarSeveridade(evaluation.RuleId)));
            }
        }

        var summary = failures == 0
            ? $"Arquivo DIMP '{document.Nome}' analisado com sucesso pelo Agente Fiscal DIMP. Nenhuma inconsistencia detectada em relacao ao Ato COTEPE/ICMS 65/2018."
            : $"Arquivo DIMP '{document.Nome}' analisado: {failures} inconsistencia(s) detectada(s) segundo as regras do Ato COTEPE/ICMS 65/2018.";
        var analysis = _questions?.Answer(solicitacao.Pergunta, questionScope, questionSamples, questionCount) ?? summary;
        var notes = new List<string>
        {
            $"Total de regras avaliadas: {evaluated}.",
            $"Total de inconsistencias: {failures}."
        };
        if (failures > findings.Count)
            notes.Add($"São exibidos os primeiros {findings.Count} achados. A contagem e as perguntas por linha consideram o arquivo inteiro.");

        return ResultadoAnaliseSofia.CriarSucesso(solicitacao.IdExecucao, solicitacao.IdSolicitacao, analysis, findings, notes);

        async IAsyncEnumerable<DimpRecord> ReadUploadRecords([EnumeratorCancellation] CancellationToken ct)
        {
            var foundHeader = false;
            await foreach (var record in _parser.ReadAsync(stream, ct).ConfigureAwait(false))
            {
                if (!foundHeader && !record.Fields.IsEmpty)
                {
                    if (record.Fields[0] != "0000")
                        throw new InvalidDataException("O primeiro registro do arquivo deve ser o 0000.");
                    foundHeader = true;
                }
                // The validation engine emits line-scoped evaluations before advancing to the next record.
                seen.Clear();
                yield return record;
            }
            if (!foundHeader) throw new InvalidDataException("Registro de abertura 0000 não encontrado.");
        }
    }

    private static ResultadoAnaliseSofia EncodingError(SolicitacaoAnaliseSofia solicitacao) =>
        ResultadoAnaliseSofia.CriarErro(
            solicitacao.IdExecucao, solicitacao.IdSolicitacao, "CODIFICACAO_DIMP_INVALIDA",
            "O texto possui caracteres fora da codificacao ISO-8859-1.",
            "Nao foi possivel validar o documento com a codificacao DIMP.");

    private static string DeterminarSeveridade(string ruleId)
    {
        if (ruleId.Contains("FILE.STRUCTURE", StringComparison.OrdinalIgnoreCase) ||
            ruleId.Contains("0000", StringComparison.OrdinalIgnoreCase) ||
            ruleId.Contains("9999", StringComparison.OrdinalIgnoreCase))
        {
            return "critica";
        }

        if (ruleId.Contains("VALUES", StringComparison.OrdinalIgnoreCase) ||
            ruleId.Contains("TOTAL", StringComparison.OrdinalIgnoreCase) ||
            ruleId.Contains("SUM", StringComparison.OrdinalIgnoreCase))
        {
            return "alta";
        }

        return "media";
    }
}
