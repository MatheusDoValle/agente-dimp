using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Api.Agente.Application.Contracts;
using AgenteDIMP.IntegrationTests.Foundation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US1;

// [Trait("Category","SpecificationPending")]
public class AnalysisSubmissionContractTests
{
    internal static WebApplicationFactory<Api.Agente.Program> Host(string tempRoot,string? connection=null) =>
        new WebApplicationFactory<Api.Agente.Program>().WithWebHostBuilder(b=>b.UseEnvironment("LocalDeterministic")
            .ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["TemporaryStorage:RootPath"]=tempRoot,
                ["ConnectionStrings:AgentDb"]=connection ?? "Host=127.0.0.1;Port=1;Database=agente;Username=agente_app;Timeout=1;Command Timeout=1;Pooling=false"
            })));
    internal static MultipartFormDataContent Upload(byte[] bytes)
    {
        var content=new MultipartFormDataContent();var file=new ByteArrayContent(bytes);
        file.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file,"file","sintetico.dimp");return content;
    }
    internal static byte[] Fixture()=>File.ReadAllBytes(Path.Combine(FoundationSupport.RepositoryRoot(),"tests/fixtures/dimp-v10/zerado-sintetico.dimp"));
    [Fact]
    public async Task EmptyFileIs422BeforeAttemptingPersistence()
    {
        using var area=new TestArtifactArea();await using var host=Host(area.Root);using var client=host.CreateClient();
        using var content=Upload([]);using var response=await client.PostAsync("/api/v1/analises",content);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,response.StatusCode);
        var problem=ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Null(problem.ExecutionId);
    }
    [Fact]
    public async Task IncompleteMultipartCannotBeAcceptedAndLeavesNoOriginal()
    {
        using var area=new TestArtifactArea();await using var host=Host(area.Root);using var client=host.CreateClient();
        using var content=new ByteArrayContent(Encoding.ASCII.GetBytes("--synthetic\r\nContent-Disposition: form-data; name=\"file\"; filename=\"sintetico.dimp\"\r\n\r\n|0000|10|"));
        content.Headers.ContentType=MediaTypeHeaderValue.Parse("multipart/form-data; boundary=synthetic");
        using var response=await client.PostAsync("/api/v1/analises",content);
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        Assert.False(Directory.Exists(area.Root) && Directory.EnumerateFiles(area.Root,"*",SearchOption.AllDirectories)
            .Any(p=>Path.GetExtension(p) is ".original" or ".upload"));
    }
    [Fact]
    public async Task UnavailableDurableStoreReturns503InsteadOf202()
    {
        using var area=new TestArtifactArea();await using var host=Host(area.Root);using var client=host.CreateClient();
        using var content=Upload(Fixture());using var response=await client.PostAsync("/api/v1/analises",content);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var problem=ContractJson.Deserialize<Problem>(await response.Content.ReadAsStringAsync());
        Assert.Null(problem.ExecutionId);Assert.DoesNotContain("Port=1",problem.Message,StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task KestrelLimitIsAnEnvironmentLimitAndReturns413()
    {
        using var area=new TestArtifactArea();
        await using var host=Host(area.Root).WithWebHostBuilder(b=>b.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=128));
        host.UseKestrel(0);using var client=host.CreateClient();using var content=Upload(Fixture());
        using var response=await client.PostAsync("/api/v1/analises",content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,response.StatusCode);
        // 128 is deliberately a test-host limit, never a product configuration.
    }
}

[Collection("FoundationDatabase")]
[Trait("Category","Database")]
// [Trait("Category","SpecificationPending")]
public class DurableAnalysisSubmissionContractTests(FoundationDatabaseFixture fixture)
{
    [Fact]
    public async Task AcceptedResponseReferencesExecutionAndJobAlreadyCommittedTogether()
    {
        using var area=new TestArtifactArea();await using var host=AnalysisSubmissionContractTests.Host(area.Root,fixture.AgentConnection);
        using var client=host.CreateClient();using var content=AnalysisSubmissionContractTests.Upload(AnalysisSubmissionContractTests.Fixture());
        using var response=await client.PostAsync("/api/v1/analises",content);
        Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
        var accepted=ContractJson.Deserialize<Accepted>(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty,accepted.ExecutionId);Assert.StartsWith("/api/v1/analises/",accepted.StatusUrl);
        Assert.Equal(accepted.StatusUrl,response.Headers.Location?.OriginalString);
        await using var db=fixture.CreateAgent();Assert.True(await db.Executions.AnyAsync(e=>e.Id==accepted.ExecutionId));
        await using var connection=new NpgsqlConnection(fixture.AgentConnection);await connection.OpenAsync();
        await using var command=new NpgsqlCommand("SELECT count(*) FROM agente.processing_jobs WHERE \"ExecutionId\"=@id",connection);
        command.Parameters.AddWithValue("id",accepted.ExecutionId);Assert.Equal(1L,await command.ExecuteScalarAsync());
        using var status=await client.GetAsync(accepted.StatusUrl);Assert.Equal(HttpStatusCode.OK,status.StatusCode);
        var state=ContractJson.Deserialize<ExecutionStatus>(await status.Content.ReadAsStringAsync());
        Assert.Equal(accepted.ExecutionId,state.ExecutionId);
    }
    [Fact]
    public async Task ClientDisconnectAfterAcceptanceDoesNotCancelDurableProcessing()
    {
        using var area=new TestArtifactArea();await using var host=AnalysisSubmissionContractTests.Host(area.Root,fixture.AgentConnection);
        Guid id;string statusUrl;
        using(var client=host.CreateClient())
        {
            using var cancellation=new CancellationTokenSource();
            using var content=AnalysisSubmissionContractTests.Upload(AnalysisSubmissionContractTests.Fixture());
            using var response=await client.PostAsync("/api/v1/analises",content,cancellation.Token);
            Assert.Equal(HttpStatusCode.Accepted,response.StatusCode);
            var accepted=ContractJson.Deserialize<Accepted>(await response.Content.ReadAsStringAsync());
            id=accepted.ExecutionId;statusUrl=accepted.StatusUrl;cancellation.Cancel();
        }
        using var secondClient=host.CreateClient();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        ExecutionStatus state;
        do
        {
            using var response=await secondClient.GetAsync(statusUrl,deadline.Token);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            state=ContractJson.Deserialize<ExecutionStatus>(await response.Content.ReadAsStringAsync(deadline.Token));
            Assert.Equal(id,state.ExecutionId);
            if(state.EndedAt is null)await Task.Delay(50,deadline.Token);
        }while(state.EndedAt is null);
        Assert.NotEqual(ExecutionStatusStatusValue.Cancelada,state.Status);
        Assert.False(state.ReportAvailable && state.ReportExpiresAt is null);
    }
}
