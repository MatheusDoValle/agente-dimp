using Api.Agente.Application.Privacy;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.Persistence;
using Api.RAG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Foundation;

public static class FoundationSupport
{
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenteDIMP.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Raiz de testes nao encontrada.");
    }
}

[CollectionDefinition("FoundationDatabase", DisableParallelization = true)]
public sealed class FoundationDatabaseCollection : ICollectionFixture<FoundationDatabaseFixture> { }

public sealed class FoundationDatabaseFixture : IAsyncLifetime
{
    public string AgentConnection { get; } = ReadLocalConnection("DIMP_TEST_AGENT_CONNECTION", "agente", "agente_app");
    public string RagConnection { get; } = ReadLocalConnection("DIMP_TEST_RAG_CONNECTION", "rag", "rag_app");
    public AgentDbContext CreateAgent(ContentSanitizer? sanitizer = null) =>
        new(new DbContextOptionsBuilder<AgentDbContext>().UseNpgsql(AgentConnection).Options, sanitizer);
    public KnowledgeDbContext CreateRag() => new(new DbContextOptionsBuilder<KnowledgeDbContext>().UseNpgsql(RagConnection).Options);

    public async Task InitializeAsync()
    {
        await using var agent = CreateAgent();
        await agent.Database.MigrateAsync();
        await using var rag = CreateRag();
        await rag.Database.MigrateAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private static string ReadLocalConnection(string variable, string database, string username)
    {
        var connection = Environment.GetEnvironmentVariable(variable);
        if (Environment.GetEnvironmentVariable("DIMP_TEST_MODE") != "compose-local" || string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Executar infra/Test-Foundation.ps1 para habilitar bancos locais isolados.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        if (builder.Host != "127.0.0.1" || builder.Port != 55432 || builder.Database != database || builder.Username != username)
            throw new InvalidOperationException("Teste de fundacao aceita somente bancos/papeis locais esperados.");
        builder.IncludeErrorDetail = false;
        builder.LogParameters = false;
        return builder.ConnectionString;
    }
}

public sealed class TestArtifactArea : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "AgenteDIMP.Foundation-" + Guid.NewGuid().ToString("N"));
    public ContentSanitizer CreateSanitizer(TimeProvider? clock = null) =>
        new(new TemporaryStorageOptions { RootPath = Root }, clock ?? TimeProvider.System);
    public void Dispose()
    {
        var absolute = Path.GetFullPath(Root);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(absolute).StartsWith("AgenteDIMP.Foundation-", StringComparison.Ordinal))
            throw new InvalidOperationException("Destino de limpeza de teste invalido.");
        if (Directory.Exists(absolute)) Directory.Delete(absolute, recursive: true);
    }
}

public sealed class ControlledClock(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}
