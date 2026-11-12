using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.Agente.Application.Sofia;

public sealed record SolicitacaoAnaliseSofia(
    [property: Required] string VersaoContrato,
    [property: Required] Guid IdExecucao,
    [property: Required] Guid IdSolicitacao,
    [property: Required] UsuarioSofia Usuario,
    [property: Required] string Pergunta,
    [property: Required] IReadOnlyList<DocumentoSofia> Documentos,
    ContextoSofia? Contexto = null
)
{
    public const string VersaoEsperada = "1.0";

    public void Validate()
    {
        if (VersaoContrato != VersaoEsperada)
            throw new ValidationException($"Versao do contrato invalida: '{VersaoContrato}'. Esperada: '{VersaoEsperada}'.");

        if (IdExecucao == Guid.Empty)
            throw new ValidationException("idExecucao obrigatorio.");

        if (IdSolicitacao == Guid.Empty)
            throw new ValidationException("idSolicitacao obrigatorio.");

        if (Usuario == null || string.IsNullOrWhiteSpace(Usuario.Nome))
            throw new ValidationException("usuario.nome obrigatorio.");

        if (string.IsNullOrWhiteSpace(Pergunta))
            throw new ValidationException("pergunta obrigatoria.");

        if (Documentos == null || Documentos.Count == 0)
            throw new ValidationException("documentos deve conter ao menos um documento.");

        foreach (var doc in Documentos)
        {
            if (string.IsNullOrWhiteSpace(doc.IdDocumento))
                throw new ValidationException("idDocumento obrigatorio.");
            if (string.IsNullOrWhiteSpace(doc.Nome))
                throw new ValidationException("nome do documento obrigatorio.");
            if (doc.Texto == null)
                throw new ValidationException("texto do documento nao pode ser nulo.");
        }
    }
}

public sealed record UsuarioSofia(
    [property: Required] string Nome
);

public sealed record DocumentoSofia(
    [property: Required] string IdDocumento,
    [property: Required] string Nome,
    string? Tipo = null,
    [property: Required] string Texto = ""
);

public sealed record ContextoSofia(
    string? Idioma = null,
    string? Instrucoes = null
);

public sealed record ResultadoAnaliseSofia(
    [property: Required] string VersaoContrato,
    [property: Required] Guid IdExecucao,
    [property: Required] Guid IdSolicitacao,
    [property: Required] AgenteSofia Agente,
    [property: Required] string Status,
    [property: Required] string Analise,
    [property: Required] IReadOnlyList<AchadoSofia> Achados,
    IReadOnlyList<string>? Observacoes = null,
    ErroSofia? Erro = null
)
{
    public const string VersaoEsperada = "1.0";
    public const string StatusConcluido = "concluido";
    public const string StatusParcial = "parcial";
    public const string StatusErro = "erro";

    public static ResultadoAnaliseSofia CriarSucesso(
        Guid idExecucao,
        Guid idSolicitacao,
        string analise,
        IReadOnlyList<AchadoSofia> achados,
        IReadOnlyList<string>? observacoes = null)
    {
        return new ResultadoAnaliseSofia(
            VersaoContrato: VersaoEsperada,
            IdExecucao: idExecucao,
            IdSolicitacao: idSolicitacao,
            Agente: AgenteSofia.PadraoDimp,
            Status: StatusConcluido,
            Analise: analise,
            Achados: achados ?? Array.Empty<AchadoSofia>(),
            Observacoes: observacoes ?? Array.Empty<string>(),
            Erro: null
        );
    }

    public static ResultadoAnaliseSofia CriarErro(
        Guid idExecucao,
        Guid idSolicitacao,
        string codigo,
        string mensagem,
        string analise)
    {
        return new ResultadoAnaliseSofia(
            VersaoContrato: VersaoEsperada,
            IdExecucao: idExecucao,
            IdSolicitacao: idSolicitacao,
            Agente: AgenteSofia.PadraoDimp,
            Status: StatusErro,
            Analise: analise,
            Achados: Array.Empty<AchadoSofia>(),
            Observacoes: Array.Empty<string>(),
            Erro: new ErroSofia(codigo, mensagem)
        );
    }
}

public sealed record AgenteSofia(
    [property: Required] string IdAgente,
    [property: Required] string Nome
)
{
    public static readonly AgenteSofia PadraoDimp = new("agente-dimp", "Agente Fiscal DIMP INFIS");
}

public sealed record AchadoSofia(
    [property: Required] string IdAchado,
    [property: Required] string Descricao,
    [property: Required] IReadOnlyList<string> IdDocumentos,
    [property: Required] IReadOnlyList<string> Evidencias,
    string? Severidade = null
);

public sealed record ErroSofia(
    [property: Required] string Codigo,
    [property: Required] string Mensagem
);
