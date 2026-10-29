using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.RAG.Infrastructure.Persistence;

public sealed class KnowledgeDbContextFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__KnowledgeDb")
            ?? "Host=127.0.0.1;Port=55432;Database=rag;Username=rag_app";
        return new KnowledgeDbContext(new DbContextOptionsBuilder<KnowledgeDbContext>().UseNpgsql(connection).Options);
    }
}
