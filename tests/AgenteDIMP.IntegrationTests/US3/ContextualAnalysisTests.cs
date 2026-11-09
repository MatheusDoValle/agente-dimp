using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Application.AI;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.AI;
using Api.Agente.Infrastructure.Rag;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US3;

public class ContextualAnalysisTests
{
    private sealed class StubRagMcpClient(McpSearchResult searchResult) : IRagMcpClient
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

    private sealed class StubOpenAiClient(AnalyticContent? content) : IOpenAiAnalysisClient
    {
        public Task<AnalyticContent?> GenerateAnalysisAsync(BuiltAnalysisPrompt prompt, Guid executionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(content);
    }

    private sealed class FailingOpenAiClient : IOpenAiAnalysisClient
    {
        public Task<AnalyticContent?> GenerateAnalysisAsync(BuiltAnalysisPrompt prompt, Guid executionId, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Simulated provider outage");
    }

    [Fact]
    public async Task ContextualAnalysis_WithValidCitation_EnrichesReportCorrectly()
    {
        var executionId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();

        var finding = new Api.Agente.Domain.Validation.Finding(
            executionId: executionId,
            ruleId: "REG_0000_FIN_INV",
            category: FindingCategory.BusinessInconsistency,
            severity: FindingSeverity.Error,
            origin: FindingOrigin.Deterministic,
            description: new MaskedText(executionId, "Finalidade informada diverge do manual"),
            location: new DocumentLocation(1, 10, "0000", 3, null),
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
                    Pages: [2],
                    Section: "0000",
                    RecordCodes: ["0000"],
                    FieldCodes: ["FIN_ARQ"],
                    Text: "Regra oficial da finalidade do arquivo DIMP.",
                    RankingMetric: new McpRankingMetric("cosine_distance", 0.02))
            ],
            LimitationCodes: [],
            UsageReferences: []);

        var llmContent = new AnalyticContent
        {
            Summary = "Inconsistência de finalidade confirmada pelo manual.",
            Explanations =
            [
                new AnalyticContentExplanationsItem
                {
                    FindingIds = [finding.Id.ToString()],
                    EvidenceIds = [evidenceId.ToString()],
                    Text = "O campo deve ser preenchido exclusivamente com 1, 2, 3 ou 4 conforme capitulo 2 do manual."
                }
            ],
            Indications = [],
            Limitations = [],
            RequiresHumanReview = true
        };

        var stage = new ContextualAnalysisStage(
            new StubRagMcpClient(mcpResult),
            new AnalysisContextBuilder(),
            new StubOpenAiClient(llmContent),
            new AnalyticResponseValidator(),
            mongoStore: null,
            NullLogger<ContextualAnalysisStage>.Instance);

        var analyticResult = await stage.ExecuteAsync(
            executionId,
            "10",
            "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
            "text-embedding-3-small-1536",
            [finding],
            systemRequiresHumanReview: true);

        Assert.NotNull(analyticResult);
        Assert.Single(analyticResult.Explanations);
        Assert.Equal(finding.Id.ToString(), analyticResult.Explanations[0].FindingIds[0]);
        Assert.Equal(evidenceId.ToString(), analyticResult.Explanations[0].EvidenceIds[0]);

        // Compõe relatório com os achados determinísticos e a análise
        var execution = new Execution(executionId, Guid.NewGuid(), "operador_teste", "contexto_local", DateTimeOffset.UtcNow);
        execution.Start(DateTimeOffset.UtcNow);
        execution.SetSource("10", "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a", "v1.0", Guid.NewGuid());
        execution.SetVerdict(ExecutionVerdict.WithInconsistencies, false);

        var contractReport = ReportComposer.Compose(
            execution,
            [],
            [finding],
            [],
            analyticResult,
            null);

        Assert.NotNull(contractReport.Analysis);
        Assert.Equal("Inconsistência de finalidade confirmada pelo manual.", contractReport.Analysis.Summary);
        Assert.Single(contractReport.Analysis.Explanations);
        Assert.True(contractReport.RequiresHumanReview);
        Assert.Equal(1, contractReport.FindingsCount);
    }

    [Fact]
    public async Task ContextualAnalysis_WhenAiProviderFails_PreservesDeterministicReport()
    {
        var executionId = Guid.NewGuid();
        var finding = new Api.Agente.Domain.Validation.Finding(
            executionId: executionId,
            ruleId: "REG_0000_ERRO",
            category: FindingCategory.BusinessInconsistency,
            severity: FindingSeverity.Error,
            origin: FindingOrigin.Deterministic,
            description: new MaskedText(executionId, "Erro determinístico comprovado"),
            location: new DocumentLocation(1, 10, "0000", 2, null),
            evidenceIds: [Guid.NewGuid()]);

        var stage = new ContextualAnalysisStage(
            new StubRagMcpClient(new McpSearchResult(Guid.NewGuid(), [], [], [])),
            new AnalysisContextBuilder(),
            new FailingOpenAiClient(),
            new AnalyticResponseValidator(),
            mongoStore: null,
            NullLogger<ContextualAnalysisStage>.Instance);

        var analyticResult = await stage.ExecuteAsync(
            executionId,
            "10",
            "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
            "text-embedding-3-small-1536",
            [finding],
            systemRequiresHumanReview: true);

        // Degradação graciosa
        Assert.Null(analyticResult);

        // O relatório determinístico permanece preservado
        var execution = new Execution(executionId, Guid.NewGuid(), "operador_teste", "contexto_local", DateTimeOffset.UtcNow);
        execution.Start(DateTimeOffset.UtcNow);
        execution.SetSource("10", "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a", "v1.0", Guid.NewGuid());
        execution.SetVerdict(ExecutionVerdict.WithInconsistencies, false);
        execution.SetLimitations(["AI_ANALYSIS_SKIPPED"]);

        var contractReport = ReportComposer.Compose(
            execution,
            [],
            [finding],
            [],
            null, // Sem analise IA
            null);

        Assert.Null(contractReport.Analysis);
        Assert.Equal(1, contractReport.FindingsCount);
        Assert.Contains("AI_ANALYSIS_SKIPPED", contractReport.LimitationCodes);
    }
}
