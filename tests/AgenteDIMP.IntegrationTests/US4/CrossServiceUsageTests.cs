using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Usage;
using Api.Agente.Infrastructure.Rag;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US4;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class CrossServiceUsageTests(FoundationDatabaseFixture fixture)
{
    private static async Task CleanDatabaseAsync(FoundationDatabaseFixture fix)
    {
        await using var agentDb = fix.CreateAgent();
        await agentDb.Database.ExecuteSqlRawAsync("DELETE FROM agente.provider_call_attempts;");
        await agentDb.Database.ExecuteSqlRawAsync("DELETE FROM agente.price_books;");

        await using var ragDb = fix.CreateRag();
        await ragDb.Database.ExecuteSqlRawAsync("DELETE FROM rag.provider_call_attempts;");
    }

    [Fact]
    public async Task EmbeddingAttempts_BelongToRagLedger_AndAreNotReadDirectlyFromAgentDbContext()
    {
        await CleanDatabaseAsync(fixture);

        var ingestionJobId = Guid.NewGuid();
        var ragAttemptRequest = new Api.RAG.Infrastructure.Usage.ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: null,
            IngestionJobId: ingestionJobId,
            Operation: Api.RAG.Infrastructure.Usage.ProviderOperation.IngestionEmbedding,
            Provider: "openai",
            Model: "text-embedding-3-small",
            AttemptNumber: 1,
            Reason: Api.RAG.Infrastructure.Usage.AttemptReason.Initial);

        await using (var ragDb = fixture.CreateRag())
        {
            var ragStore = new Api.RAG.Infrastructure.Usage.ProviderCallAttemptStore(ragDb, TimeProvider.System);
            var attempt = await ragStore.BeginAsync(ragAttemptRequest);
            await ragStore.CompleteAsync(attempt, new Api.RAG.Infrastructure.Usage.UsageMeasurement(null, null, null, 1500), "text-embedding-3-small", "req-rag-1");

            var savedRagAttempt = await ragDb.ProviderCallAttempts.SingleOrDefaultAsync(a => a.AttemptId == attempt.AttemptId);
            Assert.NotNull(savedRagAttempt);
            Assert.Equal("Api.RAG", savedRagAttempt.ServiceOwner);
            Assert.Equal(1500, savedRagAttempt.EmbeddingTokens);
        }

        // Api.Agente database context must have ZERO knowledge of rag attempts
        await using (var agentDb = fixture.CreateAgent())
        {
            var agentAttempts = await agentDb.ProviderCallAttempts.ToListAsync();
            Assert.Empty(agentAttempts);
        }
    }

    [Fact]
    public async Task ExecutionUsageAggregator_CombinesAgentAttemptsAndMcpReferences_WithoutDuplication()
    {
        await CleanDatabaseAsync(fixture);

        var executionId = Guid.NewGuid();
        var priceBook = new PriceBookVersion
        {
            Source = "official-openai-api",
            QueriedAt = DateTimeOffset.UtcNow,
            ValidFrom = DateTimeOffset.UtcNow,
            Currency = "USD",
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

        var agentAttemptRequest = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: executionId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: "openai",
            Model: "gpt-4o",
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        await using (var agentDb = fixture.CreateAgent())
        {
            agentDb.PriceBooks.Add(priceBook);

            var agentStore = new ProviderCallAttemptStore(agentDb, TimeProvider.System);
            var attempt = await agentStore.BeginAsync(agentAttemptRequest);
            await agentStore.CompleteAsync(attempt, new UsageMeasurement(10000, 2000, null, null), "gpt-4o", "req-agent-1");
        }

        // Simulate MCP references from RAG queries
        var mcpAttemptId1 = Guid.NewGuid();
        var mcpAttemptId2 = Guid.NewGuid();

        var mcpReferences = new List<McpUsageReference>
        {
            new(mcpAttemptId1, "Api.RAG", "reported", null, null, null, 5000),
            new(mcpAttemptId2, "Api.RAG", "reported", null, null, null, 3000),
            // Duplicate of attempt 1 to test deduplication!
            new(mcpAttemptId1, "Api.RAG", "reported", null, null, null, 5000)
        };

        await using (var agentDb = fixture.CreateAgent())
        {
            var estimator = new PriceBookCostEstimator(priceBook);
            var aggregator = new ExecutionUsageAggregator(agentDb, estimator);

            var summary = await aggregator.AggregateExecutionUsageAsync(executionId, mcpReferences);

            Assert.Equal(10000, summary.InputTokens);
            Assert.Equal(2000, summary.OutputTokens);
            Assert.Equal(8000, summary.EmbeddingTokens); // 5000 + 3000 (deduplicated!)
            Assert.False(summary.HasUnknownUsage);

            // Cost calculation:
            // Input: 10,000 / 1M * 2.50 = 0.025
            // Output: 2,000 / 1M * 10.00 = 0.020
            // Embedding: 8,000 / 1M * 0.02 = 0.00016
            // Total = 0.04516
            Assert.NotNull(summary.EstimatedCost);
            Assert.Equal("0.04516", summary.EstimatedCost);
            Assert.Equal("USD", summary.Currency);
        }
    }

    [Fact]
    public async Task ExecutionUsageAggregator_WhenMcpHasUnknownUsage_SetsHasUnknownUsageTrue()
    {
        await CleanDatabaseAsync(fixture);

        var executionId = Guid.NewGuid();
        var mcpReferences = new List<McpUsageReference>
        {
            new(Guid.NewGuid(), "Api.RAG", "unknown", null, null, null, null)
        };

        await using (var agentDb = fixture.CreateAgent())
        {
            var estimator = new PriceBookCostEstimator(null);
            var aggregator = new ExecutionUsageAggregator(agentDb, estimator);

            var summary = await aggregator.AggregateExecutionUsageAsync(executionId, mcpReferences);

            Assert.True(summary.HasUnknownUsage);
            Assert.Null(summary.EstimatedCost);
        }
    }
}
