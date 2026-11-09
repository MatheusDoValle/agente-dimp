using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Sofia;
using Xunit;

namespace Api.Agente.UnitTests.Sofia;

public sealed class SofiaContractTests
{
    private const string ExemploRequisicaoValida = """
    {
      "versaoContrato": "1.0",
      "idExecucao": "8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e",
      "idSolicitacao": "e1b3a4cb-d729-4bd4-b796-b9c94be07b46",
      "usuario": { "nome": "Maria Oliveira" },
      "pergunta": "Verifique divergências fiscais nos documentos.",
      "documentos": [
        { "idDocumento": "doc-001", "nome": "NFe-123.xml", "tipo": "application/xml", "texto": "<NFe>conteudo</NFe>" },
        { "idDocumento": "doc-002", "nome": "pedido.txt", "tipo": "text/plain", "texto": "Pedido de compra..." }
      ],
      "contexto": { "idioma": "pt-BR", "instrucoes": "Analisar conforme especialidade." }
    }
    """;

    [Fact]
    public void Deserializar_RequisicaoValida_DevePreencherTodosOsCampos()
    {
        var requisicao = JsonSerializer.Deserialize<SolicitacaoAnaliseSofia>(ExemploRequisicaoValida, ContractJson.Options);

        Assert.NotNull(requisicao);
        Assert.Equal("1.0", requisicao.VersaoContrato);
        Assert.Equal(Guid.Parse("8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e"), requisicao.IdExecucao);
        Assert.Equal(Guid.Parse("e1b3a4cb-d729-4bd4-b796-b9c94be07b46"), requisicao.IdSolicitacao);
        Assert.Equal("Maria Oliveira", requisicao.Usuario.Nome);
        Assert.Equal("Verifique divergências fiscais nos documentos.", requisicao.Pergunta);
        Assert.Equal(2, requisicao.Documentos.Count);
        Assert.Equal("doc-001", requisicao.Documentos[0].IdDocumento);
        Assert.Equal("NFe-123.xml", requisicao.Documentos[0].Nome);
        Assert.Equal("application/xml", requisicao.Documentos[0].Tipo);
        Assert.Equal("<NFe>conteudo</NFe>", requisicao.Documentos[0].Texto);
        Assert.NotNull(requisicao.Contexto);
        Assert.Equal("pt-BR", requisicao.Contexto.Idioma);

        // Deve validar sem exceções
        requisicao.Validate();
    }

    [Fact]
    public void Validar_VersaoIncompativel_DeveLancarExcecao()
    {
        var jsonInvalido = ExemploRequisicaoValida.Replace("\"versaoContrato\": \"1.0\"", "\"versaoContrato\": \"2.0\"");
        var requisicao = JsonSerializer.Deserialize<SolicitacaoAnaliseSofia>(jsonInvalido, ContractJson.Options);

        Assert.NotNull(requisicao);
        var ex = Assert.Throws<ValidationException>(() => requisicao.Validate());
        Assert.Contains("Versao do contrato invalida: '2.0'", ex.Message);
    }

    [Fact]
    public void Validar_IdExecucaoVazio_DeveLancarExcecao()
    {
        var requisicao = new SolicitacaoAnaliseSofia(
            VersaoContrato: "1.0",
            IdExecucao: Guid.Empty,
            IdSolicitacao: Guid.NewGuid(),
            Usuario: new UsuarioSofia("Auditor"),
            Pergunta: "Analisar",
            Documentos: new[] { new DocumentoSofia("doc1", "dimp.txt", "text/plain", "|0000|...") }
        );

        var ex = Assert.Throws<ValidationException>(() => requisicao.Validate());
        Assert.Contains("idExecucao obrigatorio", ex.Message);
    }

    [Fact]
    public void Validar_DocumentosVazio_DeveLancarExcecao()
    {
        var requisicao = new SolicitacaoAnaliseSofia(
            VersaoContrato: "1.0",
            IdExecucao: Guid.NewGuid(),
            IdSolicitacao: Guid.NewGuid(),
            Usuario: new UsuarioSofia("Auditor"),
            Pergunta: "Analisar",
            Documentos: Array.Empty<DocumentoSofia>()
        );

        var ex = Assert.Throws<ValidationException>(() => requisicao.Validate());
        Assert.Contains("documentos deve conter ao menos um documento", ex.Message);
    }

    [Fact]
    public void Serializar_ResultadoAnaliseSofia_DeveConterPropriedadesDoContrato()
    {
        var idExec = Guid.NewGuid();
        var idSol = Guid.NewGuid();
        var resultado = ResultadoAnaliseSofia.CriarSucesso(
            idExec,
            idSol,
            "Análise concluída com sucesso.",
            new[]
            {
                new AchadoSofia(
                    IdAchado: "ach-001",
                    Descricao: "Divergência de valores no registro 0100.",
                    IdDocumentos: new[] { "doc-001" },
                    Evidencias: new[] { "Linha 15: Valor informado 100 difere da soma 90" },
                    Severidade: "alta"
                )
            },
            new[] { "Processamento executado com leiaute DIMP V10." }
        );

        var json = JsonSerializer.Serialize(resultado, ContractJson.Options);

        Assert.Contains("\"versaoContrato\":\"1.0\"", json);
        Assert.Contains($"\"idExecucao\":\"{idExec}\"", json);
        Assert.Contains($"\"idSolicitacao\":\"{idSol}\"", json);
        Assert.Contains("\"idAgente\":\"agente-dimp\"", json);
        Assert.Contains("\"nome\":\"Agente Fiscal DIMP\"", json);
        Assert.Contains("\"status\":\"concluido\"", json);
        Assert.Contains("\"idAchado\":\"ach-001\"", json);
        Assert.Contains("\"severidade\":\"alta\"", json);

        var deserializado = JsonSerializer.Deserialize<ResultadoAnaliseSofia>(json, ContractJson.Options);
        Assert.NotNull(deserializado);
        Assert.Equal(idExec, deserializado.IdExecucao);
        Assert.Single(deserializado.Achados);
        Assert.Null(deserializado.Erro);
    }

    [Fact]
    public void A2ARpcResponse_Criar_DeveEstruturarEnvelopeJsonRpcTaskCompleted()
    {
        var response = A2ARpcResponse.Create(
            rpcId: "rpc-99",
            taskId: "task-100",
            contextId: "ctx-200",
            artifactName: "analise-dimp-v1",
            artifactContent: "{\"versaoContrato\":\"1.0\"}"
        );

        Assert.Equal("2.0", response.JsonRpc);
        Assert.Equal("rpc-99", response.Id);
        Assert.Equal("task-100", response.Result.Id);
        Assert.Equal("completed", response.Result.Status.State);
        Assert.Single(response.Result.Artifacts);
        Assert.Equal("analise-dimp-v1", response.Result.Artifacts[0].Name);
        Assert.Equal("text", response.Result.Artifacts[0].Parts[0].Kind);
        Assert.Equal("{\"versaoContrato\":\"1.0\"}", response.Result.Artifacts[0].Parts[0].Text);
    }
}
