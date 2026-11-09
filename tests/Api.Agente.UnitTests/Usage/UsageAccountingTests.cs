using Api.Agente.Infrastructure.Usage;
using Xunit;

namespace Api.Agente.UnitTests.Usage;

public sealed class UsageAccountingTests
{
    private static readonly DateTimeOffset TestStartedAt = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TestEndedAt = new(2026, 11, 9, 12, 0, 1, TimeSpan.Zero);

    [Fact]
    public void UsageMeasurement_Validation_RejectsNegativeTokens()
    {
        var ex = Assert.Throws<ArgumentException>(() => new UsageMeasurement(-1, 10, null, null).Validate());
        Assert.Contains("Consumo negativo", ex.Message);
    }

    [Fact]
    public void UsageMeasurement_Validation_RejectsCachedTokensExceedingInputTokens()
    {
        var ex = Assert.Throws<ArgumentException>(() => new UsageMeasurement(100, 50, 150, null).Validate());
        Assert.Contains("Tokens em cache excedem tokens de entrada", ex.Message);
    }

    [Fact]
    public void ProviderCallAttempt_Complete_WithNullTokens_ResultsInUnknownUsageState_AndNullDoesNotTurnIntoZero()
    {
        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: Guid.NewGuid(),
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt = new ProviderCallAttempt(request, TestStartedAt);
        var nullUsage = new UsageMeasurement(null, null, null, null);

        attempt.Complete(nullUsage, TestEndedAt, "gpt-4o", "req-123");

        Assert.Equal(AttemptStatus.Succeeded, attempt.Status);
        Assert.Equal(UsageState.Unknown, attempt.UsageState);
        Assert.Null(attempt.TokensInput);
        Assert.Null(attempt.TokensOutput);
        Assert.Null(attempt.CachedTokens);
        Assert.Null(attempt.EmbeddingTokens);
        Assert.Null(attempt.EstimatedCost);
        Assert.Equal("unknown", attempt.EstimateStatus);
    }

    [Fact]
    public void ProviderCallAttempt_Complete_WithPartialTokens_ResultsInPartialUsageState()
    {
        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: Guid.NewGuid(),
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt = new ProviderCallAttempt(request, TestStartedAt);
        var partialUsage = new UsageMeasurement(100, null, null, null);

        attempt.Complete(partialUsage, TestEndedAt, "gpt-4o", "req-123");

        Assert.Equal(UsageState.Partial, attempt.UsageState);
        Assert.Equal(100, attempt.TokensInput);
        Assert.Null(attempt.TokensOutput);
    }

    [Fact]
    public void ProviderCallAttempt_Complete_WithFullTokens_ResultsInReportedUsageState()
    {
        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: Guid.NewGuid(),
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt = new ProviderCallAttempt(request, TestStartedAt);
        var fullUsage = new UsageMeasurement(500, 200, 50, null);

        attempt.Complete(fullUsage, TestEndedAt, "gpt-4o", "req-123");

        Assert.Equal(UsageState.Reported, attempt.UsageState);
        Assert.Equal(500, attempt.TokensInput);
        Assert.Equal(200, attempt.TokensOutput);
        Assert.Equal(50, attempt.CachedTokens);
    }

    [Fact]
    public void ProviderCallAttempt_Fail_SetsUnknownOrCancelledStatus_AndRecordsDuration()
    {
        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: Guid.NewGuid(),
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        var attempt = new ProviderCallAttempt(request, TestStartedAt);
        attempt.Fail(TestEndedAt, cancelled: false);

        Assert.Equal(AttemptStatus.Unknown, attempt.Status);
        Assert.NotNull(attempt.EndedAt);
        Assert.Equal(1000, attempt.DurationMilliseconds);
        Assert.Null(attempt.EstimatedCost);
        Assert.Equal("unknown", attempt.EstimateStatus);
    }

    [Fact]
    public void PriceBookCostEstimator_WhenPriceBookUnavailable_ProducesUnknownCostAndNullNeverTurnsIntoZero()
    {
        var estimator = new PriceBookCostEstimator(priceBook: null);
        var usage = new UsageMeasurement(1000, 500, null, null);

        var estimate = estimator.EstimateCost("openai", "gpt-4o", ProviderOperation.ChatCompletion, usage);

        Assert.Null(estimate.EstimatedCost);
        Assert.Null(estimate.Currency);
        Assert.Null(estimate.PriceBookVersionId);
        Assert.Equal("unknown", estimate.EstimateStatus);
    }

    [Fact]
    public void PriceBookCostEstimator_WhenRateMissingForModel_ProducesUnknownCostAndNullNeverTurnsIntoZero()
    {
        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = TestStartedAt,
            ValidFrom = TestStartedAt,
            Currency = "USD",
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "text-embedding-3-small",
                    EmbeddingPricePerMillion = 0.02m
                }
            ]
        };

        var estimator = new PriceBookCostEstimator(priceBook);
        var usage = new UsageMeasurement(1000, 500, null, null);

        var estimate = estimator.EstimateCost("openai", "gpt-4o", ProviderOperation.ChatCompletion, usage);

        Assert.Null(estimate.EstimatedCost);
        Assert.Equal("USD", estimate.Currency);
        Assert.Equal(priceBook.Id, estimate.PriceBookVersionId);
        Assert.Equal("unknown", estimate.EstimateStatus);
    }

    [Fact]
    public void PriceBookCostEstimator_WhenTokensAreNullOrPartial_ProducesUnknownCostAndNullNeverTurnsIntoZero()
    {
        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = TestStartedAt,
            ValidFrom = TestStartedAt,
            Currency = "USD",
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    InputPricePerMillion = 2.50m,
                    OutputPricePerMillion = 10.00m
                }
            ]
        };

        var estimator = new PriceBookCostEstimator(priceBook);
        var partialUsage = new UsageMeasurement(1000, null, null, null);

        var estimate = estimator.EstimateCost("openai", "gpt-4o", ProviderOperation.ChatCompletion, partialUsage);

        Assert.Null(estimate.EstimatedCost);
        Assert.Equal("unknown", estimate.EstimateStatus);
    }

    [Fact]
    public void PriceBookCostEstimator_WhenCachedTokensPresentWithoutCachedRate_ProducesUnknownCost()
    {
        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = TestStartedAt,
            ValidFrom = TestStartedAt,
            Currency = "USD",
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    InputPricePerMillion = 2.50m,
                    OutputPricePerMillion = 10.00m,
                    CachedPricePerMillion = null // Missing cached rate!
                }
            ]
        };

        var estimator = new PriceBookCostEstimator(priceBook);
        var cachedUsage = new UsageMeasurement(1000, 500, 200, null);

        var estimate = estimator.EstimateCost("openai", "gpt-4o", ProviderOperation.ChatCompletion, cachedUsage);

        Assert.Null(estimate.EstimatedCost);
        Assert.Equal("unknown", estimate.EstimateStatus);
    }

    [Fact]
    public void PriceBookCostEstimator_CalculatesAccurateDecimalCost_WithFullRatesAndTokens()
    {
        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = TestStartedAt,
            ValidFrom = TestStartedAt,
            Currency = "USD",
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "gpt-4o",
                    InputPricePerMillion = 2.50m,
                    OutputPricePerMillion = 10.00m,
                    CachedPricePerMillion = 1.25m
                }
            ]
        };

        var estimator = new PriceBookCostEstimator(priceBook);
        // nonCachedInput: 10000 - 2000 = 8000 -> 8000 / 1M * 2.50 = 0.0200
        // cached: 2000 -> 2000 / 1M * 1.25 = 0.0025
        // output: 500 -> 500 / 1M * 10.00 = 0.0050
        // Total expected = 0.0275
        var usage = new UsageMeasurement(10000, 500, 2000, null);

        var estimate = estimator.EstimateCost("openai", "gpt-4o", ProviderOperation.ChatCompletion, usage);

        Assert.NotNull(estimate.EstimatedCost);
        Assert.Equal(0.0275m, estimate.EstimatedCost.Value);
        Assert.Equal("USD", estimate.Currency);
        Assert.Equal(priceBook.Id, estimate.PriceBookVersionId);
        Assert.Equal("estimated", estimate.EstimateStatus);
    }

    [Fact]
    public void PriceBookCostEstimator_ForEmbedding_CalculatesAccurateDecimalCost()
    {
        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = TestStartedAt,
            ValidFrom = TestStartedAt,
            Currency = "USD",
            Rates =
            [
                new PriceBookRate
                {
                    Provider = "openai",
                    Model = "text-embedding-3-small",
                    EmbeddingPricePerMillion = 0.02m
                }
            ]
        };

        var estimator = new PriceBookCostEstimator(priceBook);
        // 50,000 / 1M * 0.02 = 0.0010
        var usage = new UsageMeasurement(null, null, null, 50000);

        var estimate = estimator.EstimateCost("openai", "text-embedding-3-small", ProviderOperation.QueryEmbedding, usage);

        Assert.NotNull(estimate.EstimatedCost);
        Assert.Equal(0.0010m, estimate.EstimatedCost.Value);
        Assert.Equal("USD", estimate.Currency);
        Assert.Equal("estimated", estimate.EstimateStatus);
    }
}
