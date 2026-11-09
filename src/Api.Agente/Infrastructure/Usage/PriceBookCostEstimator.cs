namespace Api.Agente.Infrastructure.Usage;

public sealed record CostEstimateResult(
    decimal? EstimatedCost,
    string? Currency,
    Guid? PriceBookVersionId,
    string EstimateStatus);

public interface IPriceBookCostEstimator
{
    CostEstimateResult EstimateCost(
        string provider,
        string model,
        ProviderOperation operation,
        UsageMeasurement? usage);
}

public sealed class PriceBookCostEstimator : IPriceBookCostEstimator
{
    private readonly PriceBookVersion? _priceBook;

    public PriceBookCostEstimator(PriceBookVersion? priceBook = null)
    {
        _priceBook = priceBook;
    }

    public CostEstimateResult EstimateCost(
        string provider,
        string model,
        ProviderOperation operation,
        UsageMeasurement? usage)
    {
        // Missing pricebook produces unknown cost, never zero.
        if (_priceBook == null)
            return new CostEstimateResult(null, null, null, "unknown");

        // Missing usage produces unknown cost, never zero.
        if (usage == null)
            return new CostEstimateResult(null, _priceBook.Currency, _priceBook.Id, "unknown");

        var rate = _priceBook.FindRate(provider, model);
        if (rate == null)
            return new CostEstimateResult(null, _priceBook.Currency, _priceBook.Id, "unknown");

        const decimal oneMillion = 1_000_000m;

        if (operation == ProviderOperation.ChatCompletion)
        {
            // Both input and output tokens are mandatory for chat completion; if either is null, cost is unknown.
            if (!usage.InputTokens.HasValue || !usage.OutputTokens.HasValue)
                return new CostEstimateResult(null, _priceBook.Currency, _priceBook.Id, "unknown");

            // Required rates must exist; missing rate produces unknown, never zero.
            if (!rate.InputPricePerMillion.HasValue || !rate.OutputPricePerMillion.HasValue)
                return new CostEstimateResult(null, _priceBook.Currency, _priceBook.Id, "unknown");

            decimal totalCost = 0m;
            long nonCachedInputTokens = usage.InputTokens.Value;

            if (usage.CachedTokens.HasValue && usage.CachedTokens.Value > 0)
            {
                if (!rate.CachedPricePerMillion.HasValue)
                    return new CostEstimateResult(null, _priceBook.Currency, _priceBook.Id, "unknown");

                nonCachedInputTokens -= usage.CachedTokens.Value;
                totalCost += (usage.CachedTokens.Value / oneMillion) * rate.CachedPricePerMillion.Value;
            }

            totalCost += (nonCachedInputTokens / oneMillion) * rate.InputPricePerMillion.Value;
            totalCost += (usage.OutputTokens.Value / oneMillion) * rate.OutputPricePerMillion.Value;

            return new CostEstimateResult(totalCost, _priceBook.Currency, _priceBook.Id, "estimated");
        }
        else // QueryEmbedding or IngestionEmbedding
        {
            if (!usage.EmbeddingTokens.HasValue || !rate.EmbeddingPricePerMillion.HasValue)
                return new CostEstimateResult(null, _priceBook.Currency, _priceBook.Id, "unknown");

            decimal totalCost = (usage.EmbeddingTokens.Value / oneMillion) * rate.EmbeddingPricePerMillion.Value;
            return new CostEstimateResult(totalCost, _priceBook.Currency, _priceBook.Id, "estimated");
        }
    }
}
