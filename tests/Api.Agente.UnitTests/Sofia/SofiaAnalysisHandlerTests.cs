using Api.Agente.Application.Sofia;
using Api.Agente.Domain.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Agente.UnitTests.Sofia;

public sealed class SofiaAnalysisHandlerTests
{
    private readonly SofiaAnalysisHandler _handler;

    public SofiaAnalysisHandlerTests()
    {
        var parser = new DimpParser();
        var validationEngine = new DimpValidationEngine();
        _handler = new SofiaAnalysisHandler(parser, validationEngine, NullLogger<SofiaAnalysisHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ArquivoDimpValido_DeveRetornarStatusConcluidoSemAchados()
    {
        var dimpText = """
        |0000|10|4|SP|00000000000191|INSTITUICAO SINTETICA|20260601|20260630|2|202607|
        |0001|1|
        |0005|SINTETICA|RUA SINTETICA 1|01001000|3550308|SP|CONTATO SINTETICO|1100000000|exemplo@example.invalid|
        |0990|4|
        |1001|0|
        |1990|2|
        |9001|1|
        |9900|0000|1|
        |9900|0001|1|
        |9900|0005|1|
        |9900|0990|1|
        |9900|1001|1|
        |9900|1990|1|
        |9900|9001|1|
        |9900|9900|10|
        |9900|9990|1|
        |9900|9999|1|
        |9990|13|
        |9999|19|
        """;

        var idExec = Guid.NewGuid();
        var idSol = Guid.NewGuid();
        var solicitacao = new SolicitacaoAnaliseSofia(
            VersaoContrato: "1.0",
            IdExecucao: idExec,
            IdSolicitacao: idSol,
            Usuario: new UsuarioSofia("Auditor Fiscal"),
            Pergunta: "Validar integridade do arquivo DIMP",
            Documentos: new[]
            {
                new DocumentoSofia(
                    IdDocumento: "doc-dimp-01",
                    Nome: "zerado-sintetico.dimp",
                    Tipo: "text/plain",
                    Texto: dimpText
                )
            }
        );

        var resultado = await _handler.HandleAsync(solicitacao);

        Assert.NotNull(resultado);
        Assert.Equal("1.0", resultado.VersaoContrato);
        Assert.Equal(idExec, resultado.IdExecucao);
        Assert.Equal(idSol, resultado.IdSolicitacao);
        Assert.Equal("concluido", resultado.Status);
        Assert.Equal("agente-dimp", resultado.Agente.IdAgente);
        Assert.Contains("analisado com sucesso", resultado.Analise);
        Assert.Empty(resultado.Achados);
        Assert.Null(resultado.Erro);
    }

    [Fact]
    public async Task HandleAsync_ArquivoDimpComInconsistencia_DeveRetornarAchados()
    {
        // Registro 0000 com quantidade de campos faltantes
        var dimpComErro = """
        |0000|10|
        |9999|2|
        """;

        var idExec = Guid.NewGuid();
        var idSol = Guid.NewGuid();
        var solicitacao = new SolicitacaoAnaliseSofia(
            VersaoContrato: "1.0",
            IdExecucao: idExec,
            IdSolicitacao: idSol,
            Usuario: new UsuarioSofia("Auditor"),
            Pergunta: "Verificar inconsistências",
            Documentos: new[]
            {
                new DocumentoSofia("doc-err", "invalido.dimp", "text/plain", dimpComErro)
            }
        );

        var resultado = await _handler.HandleAsync(solicitacao);

        Assert.NotNull(resultado);
        Assert.Equal("concluido", resultado.Status);
        Assert.NotEmpty(resultado.Achados);
        Assert.Contains("inconsistencia(s) detectada(s)", resultado.Analise);

        var achado = resultado.Achados[0];
        Assert.StartsWith("ach-", achado.IdAchado);
        Assert.Contains("doc-err", achado.IdDocumentos);
        Assert.NotEmpty(achado.Evidencias);
        Assert.NotNull(achado.Severidade);
    }

    [Fact]
    public async Task HandleAsync_DocumentoNaoDimp_DeveRetornarAnaliseInformativaSemAchados()
    {
        var documentoNaoDimp = """
        <nfeProc versao="4.00">
          <NFe>
            <infNFe Id="NFe35260100000000000191550010000000011000000010">
              <total><vNF>1500.00</vNF></total>
            </infNFe>
          </NFe>
        </nfeProc>
        """;

        var solicitacao = new SolicitacaoAnaliseSofia(
            VersaoContrato: "1.0",
            IdExecucao: Guid.NewGuid(),
            IdSolicitacao: Guid.NewGuid(),
            Usuario: new UsuarioSofia("Auditor"),
            Pergunta: "Analisar documento",
            Documentos: new[]
            {
                new DocumentoSofia("doc-nfe", "nfe-123.xml", "application/xml", documentoNaoDimp)
            }
        );

        var resultado = await _handler.HandleAsync(solicitacao);

        Assert.NotNull(resultado);
        Assert.Equal("concluido", resultado.Status);
        Assert.Contains("Nenhum arquivo no formato fiscal DIMP", resultado.Analise);
        Assert.Empty(resultado.Achados);
    }
}
