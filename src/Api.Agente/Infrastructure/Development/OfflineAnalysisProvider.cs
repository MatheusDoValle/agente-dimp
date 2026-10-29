namespace Api.Agente.Infrastructure.Development;

// Development double only. The production analytical contract is implemented in US3.
public sealed class OfflineAnalysisProvider
{
    public Task<OfflineAnalysisResult> AnalyzeAsync(string maskedSummary, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(maskedSummary);
        return Task.FromResult(new OfflineAnalysisResult(
            "Resposta sintetica de desenvolvimento; nenhuma regra DIMP ou analise de IA foi executada.",
            IsSynthetic: true));
    }
}

public sealed record OfflineAnalysisResult(string Summary, bool IsSynthetic);
