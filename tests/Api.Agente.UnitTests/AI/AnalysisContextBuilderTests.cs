using Api.Agente.Application.AI;
using Api.Agente.Domain.Privacy;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Rag;
using Xunit;

namespace Api.Agente.UnitTests.AI;

public class AnalysisContextBuilderTests
{
    [Fact]
    public void BuildPrompt_FormatsFindingsAndEvidencesCorrectly()
    {
        var executionId = Guid.NewGuid();
        var findingId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();

        var finding = new Finding(
            executionId: executionId,
            ruleId: "REG_0000_FINALIDADE",
            category: FindingCategory.BusinessInconsistency,
            severity: FindingSeverity.Error,
            origin: FindingOrigin.Deterministic,
            description: new MaskedText(executionId, "Finalidade invalida informada"),
            location: new DocumentLocation(2, 10, "0000", 3, null),
            evidenceIds: [evidenceId]);

        var hit = new McpHit(
            ChunkId: evidenceId,
            DocumentId: Guid.NewGuid(),
            DocumentRevisionSha256: "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
            LayoutVersion: "10",
            EmbeddingProfileId: "text-embedding-3-small-1536",
            Pages: [2, 3],
            Section: "REGISTRO 0000",
            RecordCodes: ["0000"],
            FieldCodes: ["FIN_ARQ"],
            Text: "Valores validos para finalidade do arquivo: 1, 2, 3 ou 4.",
            RankingMetric: new McpRankingMetric("cosine_distance", 0.05));

        var builder = new AnalysisContextBuilder();
        var result = builder.BuildPrompt(
            executionId,
            "10",
            "fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a",
            [finding],
            [hit]);

        Assert.Contains(finding.Id.ToString(), result.UserPrompt);
        Assert.Contains("REG_0000_FINALIDADE", result.UserPrompt);
        Assert.Contains(evidenceId.ToString(), result.UserPrompt);
        Assert.Contains("Valores validos para finalidade", result.UserPrompt);

        Assert.Single(result.AllowedFindingIds);
        Assert.Equal(finding.Id, result.AllowedFindingIds[0]);

        Assert.Single(result.AllowedEvidenceChunkIds);
        Assert.Equal(evidenceId, result.AllowedEvidenceChunkIds[0]);
    }
}
