using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AgenteDIMP.IntegrationTests.Foundation;
using Api.RAG.Application.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US3;

[Collection("FoundationDatabase")]
public class IngestionContractTests(FoundationDatabaseFixture fixture)
{
    private WebApplicationFactory<Api.RAG.Program> CreateHost(string tempRoot) =>
        new WebApplicationFactory<Api.RAG.Program>().WithWebHostBuilder(b => b.UseEnvironment("LocalDeterministic")
            .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:KnowledgePath"] = tempRoot,
                ["ConnectionStrings:KnowledgeDb"] = fixture.RagConnection,
                ["DIMP_TEST_MODE"] = "compose-local"
            })));

    private static MultipartFormDataContent CreateUpload(
        byte[] fileBytes,
        string sourceUrl = "https://www.confaz.fazenda.gov.br/manual.pdf",
        string layoutVersion = "10",
        string? expectedSha256 = null,
        string extractionProfileId = "pdfpig-v1",
        string chunkProfileId = "chunk-layout10-section-record-v1",
        string embeddingProfileId = "local-fake-sha256-v1")
    {
        var hash = expectedSha256 ?? Convert.ToHexString(SHA256.HashData(fileBytes)).ToLowerInvariant();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        content.Add(fileContent, "file", "manual.pdf");
        content.Add(new StringContent(sourceUrl), "sourceUrl");
        content.Add(new StringContent(layoutVersion), "layoutVersion");
        content.Add(new StringContent(hash), "expectedSha256");
        content.Add(new StringContent(extractionProfileId), "extractionProfileId");
        content.Add(new StringContent(chunkProfileId), "chunkProfileId");
        content.Add(new StringContent(embeddingProfileId), "embeddingProfileId");

        return content;
    }

    private static byte[] SamplePdfBytes()
    {
        var pdfPath = Path.Combine(FoundationSupport.RepositoryRoot(), "docs/referencias/dimp-v10-manual-confaz.pdf");
        if (File.Exists(pdfPath)) return File.ReadAllBytes(pdfPath);
        // Fallback synthetic PDF header
        return Encoding.ASCII.GetBytes("%PDF-1.4\r\n1 0 obj\r\n<< /Type /Catalog >>\r\nendobj\r\ntrailer\r\n<< /Root 1 0 R >>\r\n%%EOF");
    }

    [Fact]
    public async Task NonPdfFile_Returns415UnsupportedMediaType()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var textBytes = Encoding.UTF8.GetBytes("Nao sou um arquivo PDF valido.");
        using var content = CreateUpload(textBytes);

        using var response = await client.PostAsync("/api/v1/documentos/ingestoes", content);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = ContractJson.Deserialize<Problem>(body);
        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", problem.Code);
    }

    [Fact]
    public async Task MissingMetadata_Returns400BadRequest()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(SamplePdfBytes()), "file", "manual.pdf");

        using var response = await client.PostAsync("/api/v1/documentos/ingestoes", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = ContractJson.Deserialize<Problem>(body);
        Assert.Equal("METADATA_MISSING", problem.Code);
    }

    [Fact]
    public async Task UnknownProfile_Returns422UnprocessableEntity()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var content = CreateUpload(
            SamplePdfBytes(),
            embeddingProfileId: "perfil-inexistente-xyz");

        using var response = await client.PostAsync("/api/v1/documentos/ingestoes", content);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = ContractJson.Deserialize<Problem>(body);
        Assert.Equal("PROFILE_UNKNOWN", problem.Code);
    }

    [Fact]
    public async Task HashMismatch_Returns422UnprocessableEntity()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var wrongHash = new string('a', 64);
        using var content = CreateUpload(
            SamplePdfBytes(),
            expectedSha256: wrongHash);

        using var response = await client.PostAsync("/api/v1/documentos/ingestoes", content);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = ContractJson.Deserialize<Problem>(body);
        Assert.Equal("HASH_MISMATCH", problem.Code);
    }

    [Fact]
    public async Task ValidPdf_Returns202Accepted_AndStatusCanBeRetrieved()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        var bytes = SamplePdfBytes();
        using var content = CreateUpload(bytes);

        using var response = await client.PostAsync("/api/v1/documentos/ingestoes", content);
        Assert.True(response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var accepted = ContractJson.Deserialize<IngestionAccepted>(body);
        Assert.NotEqual(Guid.Empty, accepted.IngestionId);
        Assert.NotEqual(Guid.Empty, accepted.DocumentId);
        Assert.StartsWith("/api/v1/documentos/ingestoes/", accepted.StatusUrl);

        // Consulta de status da ingestao
        using var statusResp = await client.GetAsync(accepted.StatusUrl);
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);

        var statusBody = await statusResp.Content.ReadAsStringAsync();
        var status = ContractJson.Deserialize<IngestionStatus>(statusBody);
        Assert.Equal(accepted.IngestionId, status.IngestionId);
        Assert.Equal(accepted.DocumentId, status.DocumentId);
    }

    [Fact]
    public async Task UnknownIngestionId_Returns404NotFound()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var response = await client.GetAsync($"/api/v1/documentos/ingestoes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = ContractJson.Deserialize<Problem>(body);
        Assert.Equal("INGESTION_NOT_FOUND", problem.Code);
    }

    [Fact]
    public async Task UnknownDocumentId_Returns404NotFoundForRevisions()
    {
        using var area = new TestArtifactArea();
        await using var host = CreateHost(area.Root);
        using var client = host.CreateClient();

        using var response = await client.GetAsync($"/api/v1/documentos/{Guid.NewGuid()}/revisoes");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var problem = ContractJson.Deserialize<Problem>(body);
        Assert.Equal("DOCUMENT_NOT_FOUND", problem.Code);
    }
}
