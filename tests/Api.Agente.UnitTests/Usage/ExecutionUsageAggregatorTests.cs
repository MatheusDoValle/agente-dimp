using Api.Agente.Application.Contracts;
using Api.Agente.Application.Usage;
using Api.Agente.Infrastructure.Rag;
using Api.Agente.Infrastructure.Usage;
using Xunit;

namespace Api.Agente.UnitTests.Usage;

public sealed class ExecutionUsageAggregatorTests
{
    [Fact]
    public void Aggregator_CombinesLocalAndMcpUsage_WithDeduplication_AndDecimalAccuracy()
    {
        var executionId = Guid.NewGuid();

        var priceBook = new PriceBookVersion
        {
            Source = "openai-official",
            Currency = "USD",
            QueriedAt = DateTimeOffset.UtcNow,
            ValidFrom = DateTimeOffset.UtcNow,
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    InputPricePerMillion = 2.50m,
                    OutputPricePerMillion = 10.00m
                },
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "text-embedding-3-small",
                    EmbeddingPricePerMillion = 0.02m
                }
            ]
        };

        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: executionId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt = new ProviderCallAttempt(request, DateTimeOffset.UtcNow);
        attempt.Complete(new UsageMeasurement(10000, 2000, null, null), DateTimeOffset.UtcNow.AddSeconds(1), "gpt-4o", "req-1");

        var mcpAttemptId = Guid.NewGuid();
        var mcpRefs = new List<McpUsageReference>
        {
            new(mcpAttemptId, "Api.RAG", "reported", null, null, null, 10000),
            // Duplicate reference to verify deduplication
            new(mcpAttemptId, "Api.RAG", "reported", null, null, null, 10000)
        };

        var estimator = new PriceBookCostEstimator(priceBook);
        var aggregator = new ExecutionUsageAggregator(null, estimator);

        var summary = aggregator.AggregateAttempts([attempt], mcpRefs);

        Assert.Equal(10000, summary.InputTokens);
        Assert.Equal(2000, summary.OutputTokens);
        Assert.Equal(10000, summary.EmbeddingTokens); // deduplicated!
        Assert.False(summary.HasUnknownUsage);

        // Cost: (10,000 / 1M * 2.50) + (2,000 / 1M * 10.00) + (10,000 / 1M * 0.02)
        // = 0.025 + 0.020 + 0.0002 = 0.0452
        Assert.Equal("0.0452", summary.EstimatedCost);
        Assert.Equal("USD", summary.Currency);
    }

    [Fact]
    public void Aggregator_WhenAnyAttemptHasUnknownTokens_SetsHasUnknownUsageTrue_AndNullCost()
    {
        var executionId = Guid.NewGuid();

        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: executionId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt = new ProviderCallAttempt(request, DateTimeOffset.UtcNow);
        attempt.Complete(new UsageMeasurement(null, null, null, null), DateTimeOffset.UtcNow.AddSeconds(1), "gpt-4o", "req-1");

        var estimator = new PriceBookCostEstimator(null);
        var aggregator = new ExecutionUsageAggregator(null, estimator);

        var summary = aggregator.AggregateAttempts([attempt]);

        Assert.True(summary.HasUnknownUsage);
        Assert.Null(summary.EstimatedCost);
    }
}
