using System.Globalization;
using Api.Agente.Application.Contracts;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Rag;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Application.Usage;

public interface IExecutionUsageAggregator
{
    Task<UsageSummary> AggregateExecutionUsageAsync(
        Guid executionId,
        IEnumerable<McpUsageReference>? mcpUsageReferences = null,
        CancellationToken cancellationToken = default);

    UsageSummary AggregateAttempts(
        IEnumerable<ProviderCallAttempt> localAttempts,
        IEnumerable<McpUsageReference>? mcpUsageReferences = null);
}

public sealed class ExecutionUsageAggregator(
    AgentDbContext? dbContext,
    IPriceBookCostEstimator costEstimator) : IExecutionUsageAggregator
{
    public async Task<UsageSummary> AggregateExecutionUsageAsync(
        Guid executionId,
        IEnumerable<McpUsageReference>? mcpUsageReferences = null,
        CancellationToken cancellationToken = default)
    {
        var localAttempts = dbContext != null
            ? await dbContext.ProviderCallAttempts.AsNoTracking()
                .Where(a => a.ExecutionId == executionId)
                .OrderBy(a => a.StartedAt)
                .ToListAsync(cancellationToken)
            : [];

        return AggregateAttempts(localAttempts, mcpUsageReferences);
    }

    public UsageSummary AggregateAttempts(
        IEnumerable<ProviderCallAttempt> localAttempts,
        IEnumerable<McpUsageReference>? mcpUsageReferences = null)
    {
        long? totalInput = null;
        long? totalOutput = null;
        long? totalEmbedding = null;
        bool hasUnknownUsage = false;
        decimal? totalCost = 0m;
        string? currency = null;
        string? priceBookVersionId = null;

        var attemptsList = localAttempts.ToList();
        foreach (var attempt in attemptsList)
        {
            if (attempt.UsageState is UsageState.Unknown or UsageState.Partial)
            {
                hasUnknownUsage = true;
            }

            if (attempt.TokensInput.HasValue)
                totalInput = (totalInput ?? 0) + attempt.TokensInput.Value;

            if (attempt.TokensOutput.HasValue)
                totalOutput = (totalOutput ?? 0) + attempt.TokensOutput.Value;

            if (attempt.EmbeddingTokens.HasValue)
                totalEmbedding = (totalEmbedding ?? 0) + attempt.EmbeddingTokens.Value;

            var cost = attempt.EstimatedCost;
            if (!cost.HasValue)
            {
                var estimate = costEstimator.EstimateCost(
                    attempt.Provider,
                    attempt.RequestedModel,
                    attempt.Operation,
                    new UsageMeasurement(attempt.TokensInput, attempt.TokensOutput, attempt.CachedTokens, attempt.EmbeddingTokens));

                cost = estimate.EstimatedCost;
                if (estimate.Currency != null) currency = estimate.Currency;
                if (estimate.PriceBookVersionId.HasValue) priceBookVersionId = estimate.PriceBookVersionId.Value.ToString();
            }
            else
            {
                if (attempt.Currency != null) currency = attempt.Currency;
                if (attempt.PriceBookVersionId.HasValue) priceBookVersionId = attempt.PriceBookVersionId.Value.ToString();
            }

            if (cost.HasValue && totalCost.HasValue)
            {
                totalCost += cost.Value;
            }
            else
            {
                totalCost = null;
            }
        }

        var seenAttemptIds = new HashSet<Guid>(attemptsList.Select(a => a.AttemptId));
        if (mcpUsageReferences != null)
        {
            foreach (var mcpRef in mcpUsageReferences)
            {
                if (!seenAttemptIds.Add(mcpRef.CallAttemptId))
                    continue;

                if (mcpRef.UsageState.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
                    mcpRef.UsageState.Equals("partial", StringComparison.OrdinalIgnoreCase))
                {
                    hasUnknownUsage = true;
                }

                if (mcpRef.TokensInput.HasValue)
                    totalInput = (totalInput ?? 0) + mcpRef.TokensInput.Value;

                if (mcpRef.TokensOutput.HasValue)
                    totalOutput = (totalOutput ?? 0) + mcpRef.TokensOutput.Value;

                if (mcpRef.EmbeddingTokens.HasValue)
                {
                    totalEmbedding = (totalEmbedding ?? 0) + mcpRef.EmbeddingTokens.Value;

                    if (totalCost.HasValue)
                    {
                        var embeddingEstimate = costEstimator.EstimateCost(
                            "openai",
                            "text-embedding-3-small",
                            ProviderOperation.QueryEmbedding,
                            new UsageMeasurement(null, null, null, mcpRef.EmbeddingTokens.Value));

                        if (embeddingEstimate.EstimatedCost.HasValue)
                        {
                            totalCost += embeddingEstimate.EstimatedCost.Value;
                            if (embeddingEstimate.Currency != null) currency = embeddingEstimate.Currency;
                            if (embeddingEstimate.PriceBookVersionId.HasValue) priceBookVersionId = embeddingEstimate.PriceBookVersionId.Value.ToString();
                        }
                        else
                        {
                            totalCost = null;
                        }
                    }
                }
            }
        }

        if (hasUnknownUsage)
        {
            totalCost = null;
        }

        string? formattedCost = totalCost.HasValue && (totalInput.HasValue || totalOutput.HasValue || totalEmbedding.HasValue)
            ? totalCost.Value.ToString("0.############", CultureInfo.InvariantCulture)
            : null;

        return new UsageSummary
        {
            InputTokens = totalInput,
            OutputTokens = totalOutput,
            EmbeddingTokens = totalEmbedding,
            HasUnknownUsage = hasUnknownUsage,
            EstimatedCost = formattedCost,
            Currency = currency,
            PriceBookVersionId = priceBookVersionId
        };
    }
}
