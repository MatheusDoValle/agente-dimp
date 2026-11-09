namespace Api.Agente.Infrastructure.Usage;

public sealed class PriceBookVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Source { get; init; } = string.Empty;
    public DateTimeOffset QueriedAt { get; init; }
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public string Currency { get; init; } = "USD";
    public string Unit { get; init; } = "per_1m_tokens";
    public List<PriceBookRate> Rates { get; init; } = [];

    public PriceBookRate? FindRate(string provider, string model) =>
        Rates.FirstOrDefault(r =>
            string.Equals(r.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Model, model, StringComparison.OrdinalIgnoreCase));
}

public sealed class PriceBookRate
{
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public decimal? InputPricePerMillion { get; init; }
    public decimal? OutputPricePerMillion { get; init; }
    public decimal? CachedPricePerMillion { get; init; }
    public decimal? EmbeddingPricePerMillion { get; init; }
}
