using Api.Agente.Domain.Privacy;

namespace Api.Agente.Domain.Reports;

public sealed class Report : IMaskedPayload
{
    private Report() { }
    public Report(Guid executionId, MaskedText summary, DateTimeOffset createdAt, MaskedText? analyticContent = null)
    {
        if (executionId == Guid.Empty || summary.ExecutionId != executionId
            || analyticContent is not null && analyticContent.ExecutionId != executionId) throw new ArgumentException("Conteudo de outra execucao.");
        ExecutionId = executionId; Summary = summary.Value; AnalyticContent = analyticContent?.Value;
        CreatedAt = createdAt.ToUniversalTime();
    }
    public Guid ExecutionId { get; private set; }
    public string ContractVersion { get; private set; } = "1.0.0";
    public string Summary { get; private set; } = "";
    public string? AnalyticContent { get; private set; }
    public bool RequiresHumanReview { get; private set; }
    public string[] LimitationCodes { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    internal void SetExpiration(DateTimeOffset endedAt) => ExpiresAt = endedAt.ToUniversalTime().AddDays(30);
    public IReadOnlyDictionary<string, string> GetMaskedFields()
    {
        var fields = new Dictionary<string, string> { [nameof(Summary)] = Summary };
        if (AnalyticContent is not null) fields[nameof(AnalyticContent)] = AnalyticContent;
        return fields;
    }
}
