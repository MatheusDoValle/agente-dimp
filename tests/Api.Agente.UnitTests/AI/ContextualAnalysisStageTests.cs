using Api.Agente.Application.AI;
using Api.Agente.Application.Contracts;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.AI;
using Api.Agente.Infrastructure.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Agente.UnitTests.AI;

public class ContextualAnalysisStageTests
{
    private sealed class FakeRagMcpClient(McpSearchResult searchResult) : IRagMcpClient
    {
        public Task<McpSearchResult> SearchManualAsync(string query, string layoutVersion, string revisionSha256, string embeddingProfileId, IReadOnlyList<string>? recordCodes = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(searchResult);

        public Task<McpChunkResult?> GetManualChunkAsync(Guid chunkId, string revisionSha256, CancellationToken cancellationToken = default) =>
            Task.FromResult<McpChunkResult?>(null);

        public Task<McpRevisionResult?> GetDocumentRevisionAsync(string revisionSha256, CancellationToken cancellationToken = default) =>
            Task.FromResult<McpRevisionResult?>(null);

        public Task<McpUsageResult?> GetIngestionUsageAsync(Guid ingestionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<McpUsageResult?>(null);
    }

    private sealed class FakeOpenAiAnalysisClient(AnalyticContent? contentToReturn) : IOpenAiAnalysisClient
    {
        public Task<AnalyticContent?> GenerateAnalysisAsync(BuiltAnalysisPrompt prompt, Guid executionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(contentToReturn);
    }

    private sealed class FailingOpenAiAnalysisClient : IOpenAiAnalysisClient
    {
        public Task<AnalyticContent?> GenerateAnalysisAsync(BuiltAnalysisPrompt prompt, Guid executionId, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("OpenAI timeout simulado.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenServicesSucceed_ReturnsValidatedAnalyticContent()
    {
        var executionId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();

        var finding = new Domain.Validation.Finding(
            executionId: executionId,
            ruleId: "REG_0000_ERRO",
            category: FindingCategory.BusinessInconsistency,
            severity: FindingSeverity.Error,
            origin: FindingOrigin.Deterministic,
            description: new MaskedText(executionId, "Erro no campo"),
            location: new DocumentLocation(1, 10, "0000", 2, null),
            evidenceIds: [evidenceId]);

        var mcpResult = new McpSearchResult(
            RetrievalId: Guid.NewGuid(),
            Hits:
            [
                new McpHit(
                    ChunkId: evidenceId,
                    DocumentId: Guid.NewGuid(),
                    DocumentRevisionSha256: "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
                    LayoutVersion: "10",
                    EmbeddingProfileId: "text-embedding-3-small-1536",
                    Pages: [1],
                    Section: "0000",
                    RecordCodes: ["0000"],
                    FieldCodes: [],
                    Text: "Regra do registro 0000",
                    RankingMetric: new McpRankingMetric("cosine_distance", 0.1))
            ],
            LimitationCodes: [],
            UsageReferences: []);

        var llmContent = new AnalyticContent
        {
            Summary = "Analise concluida.",
            Explanations =
            [
                new AnalyticContentExplanationsItem
                {
                    FindingIds = [finding.Id.ToString()],
                    EvidenceIds = [evidenceId.ToString()],
                    Text = "Explicacao de acordo com o manual oficial."
                }
            ],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = true
        };

        var stage = new ContextualAnalysisStage(
            new FakeRagMcpClient(mcpResult),
            new AnalysisContextBuilder(),
            new FakeOpenAiAnalysisClient(llmContent),
            new AnalyticResponseValidator(),
            mongoStore: null,
            NullLogger<ContextualAnalysisStage>.Instance);

        var result = await stage.ExecuteAsync(
            executionId,
            "10",
            "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
            "text-embedding-3-small-1536",
            [finding],
            systemRequiresHumanReview: true);

        Assert.NotNull(result);
        Assert.Equal("Analise concluida.", result.Summary);
        Assert.Single(result.Explanations);
        Assert.True(result.RequiresHumanReview);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAiClientFails_DegradesGracefullyWithoutThrowing()
    {
        var executionId = Guid.NewGuid();
        var finding = new Domain.Validation.Finding(
            executionId: executionId,
            ruleId: "REG_0000_ERRO",
            category: FindingCategory.BusinessInconsistency,
            severity: FindingSeverity.Error,
            origin: FindingOrigin.Deterministic,
            description: new MaskedText(executionId, "Erro no campo"),
            location: new DocumentLocation(1, 10, "0000", 2, null),
            evidenceIds: [Guid.NewGuid()]);

        var mcpResult = new McpSearchResult(Guid.NewGuid(), [], [], []);

        var stage = new ContextualAnalysisStage(
            new FakeRagMcpClient(mcpResult),
            new AnalysisContextBuilder(),
            new FailingOpenAiAnalysisClient(),
            new AnalyticResponseValidator(),
            mongoStore: null,
            NullLogger<ContextualAnalysisStage>.Instance);

        var result = await stage.ExecuteAsync(
            executionId,
            "10",
            "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
            "text-embedding-3-small-1536",
            [finding],
            systemRequiresHumanReview: true);

        // Degradação graciosa: retorna null, não quebra a pipeline
        Assert.Null(result);
    }
}
