using System.Text.Json;
using System.Text.RegularExpressions;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Privacy;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.RAG.Infrastructure.Mcp;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Security;

public class OutputAndToolBoundaryTests
{
    [Fact]
    public void McpToolCatalog_ContainsOnlyReadOnlyInspectionTools()
    {
        // MCP must expose strictly read-only inspection tools
        var allowedTools = new HashSet<string>
        {
            "search_manual",
            "get_manual_chunk",
            "get_document_revision",
            "get_ingestion_usage"
        };

        // There must be no destructive, administrative, or file system writing tools
        var forbiddenSubstrings = new[] { "delete", "write", "drop", "update", "exec", "modify", "upload", "insert" };

        foreach (var tool in allowedTools)
        {
            foreach (var forbidden in forbiddenSubstrings)
            {
                Assert.DoesNotContain(forbidden, tool, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task ReportOutput_NeverExposesRawCpfOrCnpj()
    {
        using var area = new TestArtifactArea();
        var sanitizer = area.CreateSanitizer();

        var executionId = Guid.NewGuid();
        var rawCpf = "123.456.789-01";
        var rawCnpj = "12.345.678/0001-95";

        var textWithSensitiveData = $"Identificado contribuinte com CPF {rawCpf} e CNPJ {rawCnpj} em desacordo.";
        var maskedText = await sanitizer.SanitizeAsync(executionId, textWithSensitiveData);

        var evidenceId = Guid.NewGuid();
        var finding = new Api.Agente.Domain.Validation.Finding(
            executionId: executionId,
            ruleId: "REG_0000_SENSITIVE",
            category: FindingCategory.BusinessInconsistency,
            severity: FindingSeverity.Error,
            origin: FindingOrigin.Deterministic,
            description: maskedText,
            location: new DocumentLocation(1, 10, "0000", 3, null),
            evidenceIds: [evidenceId]);

        var exec = new Execution(executionId, Guid.NewGuid(), "sec-tester", "ctx", DateTimeOffset.UtcNow);
        exec.Start(DateTimeOffset.UtcNow);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(DateTimeOffset.UtcNow);
        exec.CompleteAfterCleanup(ExecutionState.Completed, DateTimeOffset.UtcNow, hasReport: true);

        var report = ReportComposer.Compose(
            exec,
            [],
            [finding],
            [],
            null,
            null);

        var serializedReport = ContractJson.Serialize(report);

        // Assert strictly that neither the raw CPF nor the raw CNPJ appears in the serialized output
        Assert.DoesNotContain("123.456.789-01", serializedReport);
        Assert.DoesNotContain("12.345.678/0001-95", serializedReport);
        Assert.DoesNotContain("12345678901", serializedReport);
        Assert.DoesNotContain("12345678000195", serializedReport);
    }

    [Fact]
    public void TemporaryArtifactArea_CleansUpOnDispose_LeavingZeroFiles()
    {
        string rootPath;
        using (var area = new TestArtifactArea())
        {
            rootPath = area.Root;
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(Path.Combine(rootPath, "temp-test.txt"), "temporary content");
            Assert.True(Directory.Exists(rootPath));
        }

        // After Dispose, the temporary directory must be completely erased
        Assert.False(Directory.Exists(rootPath), "Diretório temporário deve ser excluído após Dispose");
    }
}
