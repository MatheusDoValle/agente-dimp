using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Foundation;

[Collection("FoundationDatabase")]
[Trait("Category", "Database")]
public class DataOwnershipTests(FoundationDatabaseFixture fixture)
{
    [Fact]
    public async Task ApplicationRolesOwnSeparateDatabasesAndVectorExistsOnlyInRag()
    {
        await using var agent = new NpgsqlConnection(fixture.AgentConnection);
        await using var rag = new NpgsqlConnection(fixture.RagConnection);
        await agent.OpenAsync(); await rag.OpenAsync();
        Assert.Equal("agente:agente_app", await new NpgsqlCommand("SELECT current_database() || ':' || current_user", agent).ExecuteScalarAsync());
        Assert.Equal("rag:rag_app", await new NpgsqlCommand("SELECT current_database() || ':' || current_user", rag).ExecuteScalarAsync());
        Assert.NotNull(await new NpgsqlCommand("SELECT extversion FROM pg_extension WHERE extname='vector'", rag).ExecuteScalarAsync());
        Assert.Null(await new NpgsqlCommand("SELECT extversion FROM pg_extension WHERE extname='vector'", agent).ExecuteScalarAsync());
    }

    [Fact]
    public async Task AgentCredentialsCannotConnectToKnowledgeDatabase()
    {
        var target = new NpgsqlConnectionStringBuilder(fixture.AgentConnection) { Database = "rag" };
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task KnowledgeCredentialsCannotConnectToAgentDatabase()
    {
        var target = new NpgsqlConnectionStringBuilder(fixture.RagConnection) { Database = "agente" };
        await using var connection = new NpgsqlConnection(target.ConnectionString);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task MigrationsAreAppliedAndHaveNoPendingModelChanges()
    {
        await using var agent = fixture.CreateAgent();
        await using var rag = fixture.CreateRag();
        Assert.NotEmpty(await agent.Database.GetAppliedMigrationsAsync());
        Assert.NotEmpty(await rag.Database.GetAppliedMigrationsAsync());
        Assert.False(agent.Database.HasPendingModelChanges());
        Assert.False(rag.Database.HasPendingModelChanges());
    }
}
