using System.Text;
using Api.Agente.Domain.Validation;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Application.Sofia;

public sealed class SofiaAnalysisHandler : ISofiaAnalysisHandler
{
    private readonly IDimpParser _parser;
    private readonly DimpValidationEngine _validationEngine;
    private readonly ILogger<SofiaAnalysisHandler> _logger;

    public SofiaAnalysisHandler(
        IDimpParser parser,
        DimpValidationEngine validationEngine,
        ILogger<SofiaAnalysisHandler> logger)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _validationEngine = validationEngine ?? throw new ArgumentNullException(nameof(validationEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            var rawBytes = Encoding.UTF8.GetBytes(dimpDoc.Texto);
            using var stream = new MemoryStream(rawBytes);

            var records = _parser.ReadAsync(stream, cancellationToken);
            var evaluations = new List<RuleEvaluation>();

            await foreach (var ev in _validationEngine.ValidateAsync(records, null, solicitacao.IdExecucao, cancellationToken).ConfigureAwait(false))
            {
                evaluations.Add(ev);
            }

            var failed = evaluations
                .Where(e => e.Result == RuleResult.Failed)
                .GroupBy(e => (e.RuleId, e.OccurrenceKey))
                .Select(g => g.First())
                .ToList();

            var achados = new List<AchadoSofia>();
            var index = 1;

            foreach (var failure in failed)
            {
                var severidade = DeterminarSeveridade(failure.RuleId);
                var achado = new AchadoSofia(
                    IdAchado: $"ach-{index++:D3}",
                    Descricao: $"Inconsistencia fiscal na regra {failure.RuleId} ({failure.OccurrenceKey}).",
                    IdDocumentos: new[] { dimpDoc.IdDocumento },
                    Evidencias: new[] { $"Falha detectada na regra {failure.RuleId} em {failure.OccurrenceKey} no documento {dimpDoc.Nome}." },
                    Severidade: severidade
                );
                achados.Add(achado);
            }

            string analise;
            if (achados.Count == 0)
            {
                analise = $"Arquivo DIMP '{dimpDoc.Nome}' analisado com sucesso pelo Agente Fiscal DIMP. Nenhuma inconsistencia detectada em relacao ao Ato COTEPE/ICMS 65/2018.";
            }
            else
            {
                analise = $"Arquivo DIMP '{dimpDoc.Nome}' analisado: {achados.Count} inconsistencia(s) detectada(s) segundo as regras do Ato COTEPE/ICMS 65/2018.";
            }

            return ResultadoAnaliseSofia.CriarSucesso(
                solicitacao.IdExecucao,
                solicitacao.IdSolicitacao,
                analise,
                achados,
                observacoes: new[]
                {
                    $"Total de regras avaliadas: {evaluations.Count}.",
                    $"Total de inconsistencias: {achados.Count}."
                }
            );
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
