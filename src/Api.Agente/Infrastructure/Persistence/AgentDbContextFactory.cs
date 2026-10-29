using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.Agente.Infrastructure.Persistence;

public sealed class AgentDbContextFactory : IDesignTimeDbContextFactory<AgentDbContext>
{
    public AgentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__AgentDb")
            ?? "Host=127.0.0.1;Port=55432;Database=agente;Username=agente_app";
        return new AgentDbContext(new DbContextOptionsBuilder<AgentDbContext>().UseNpgsql(connection).Options);
    }
}
