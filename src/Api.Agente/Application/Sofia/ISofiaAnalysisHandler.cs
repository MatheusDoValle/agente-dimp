namespace Api.Agente.Application.Sofia;

public interface ISofiaAnalysisHandler
{
    Task<ResultadoAnaliseSofia> HandleAsync(SolicitacaoAnaliseSofia solicitacao, CancellationToken cancellationToken = default);
    Task<ResultadoAnaliseSofia> HandleUploadAsync(SolicitacaoAnaliseSofia solicitacao, Stream arquivo, CancellationToken cancellationToken = default);
}
