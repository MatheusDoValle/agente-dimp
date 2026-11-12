using Api.Agente.Application.Sofia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Api.Agente.Endpoints;

public static class AgentCardEndpoint
{
    public static IEndpointRouteBuilder MapAgentCardEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/agent-card.json", (IConfiguration configuration) =>
        {
            var publicUrl = configuration["A2A:PublicUrl"] ?? "https://agente-dimp-demo.vercel.app/a2a";

            var card = new AgentCard(
                Name: "Agente Fiscal DIMP INFIS",
                Description: "Analisa e valida declaracoes e arquivos fiscais DIMP (Ato COTEPE/ICMS 65/2018) enviados pela SOFIA.",
                Url: publicUrl,
                Version: configuration["A2A:Version"] ?? "1.0.0",
                Capabilities: new AgentCapabilities(Streaming: false, PushNotifications: false),
                DefaultInputModes: new[] { "text/plain" },
                DefaultOutputModes: new[] { "text/plain" },
                Skills: new[]
                {
                    new AgentSkill(
                        Id: "analisar_documentos",
                        Name: "Validacao Fiscal DIMP V10",
                        Description: "Analisa arquivos DIMP e valida estrutura, totalizadores e regras de conformidade tributaria.",
                        Tags: new[] { "infis", "fiscal", "dimp", "sofia" }
                    )
                },
                SecuritySchemes: new Dictionary<string, AgentSecurityScheme>
                {
                    ["sofiaService"] = new("http", "bearer")
                },
                Security: new List<IReadOnlyDictionary<string, IReadOnlyList<string>>>
                {
                    new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["sofiaService"] = Array.Empty<string>()
                    }
                }
            );

            return Results.Ok(card);
        })
        .WithName("GetAgentCard")
        .WithTags("SOFIA A2A Discovery")
        .WithSummary("Obter Agent Card (descoberta A2A)")
        .WithDescription("Retorna os metadados do agente para o orquestrador SOFIA conforme especificação A2A 0.3.0. Requer autenticação Authorization: Bearer <token>.")
        .Produces<AgentCard>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireCors("SofiaBrowser");

        return endpoints;
    }
}
