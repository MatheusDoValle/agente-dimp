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
            var publicUrl = configuration["A2A:PublicUrl"] ?? "http://agente-dimp:8080/a2a";

            var card = new AgentCard(
                Name: "Agente Fiscal DIMP INFIS",
                Description: "Analisa e valida declaracoes e arquivos fiscais DIMP (Ato COTEPE/ICMS 65/2018) enviados pela SOFIA.",
                Url: publicUrl,
                Version: "1.0.0",
                Capabilities: new AgentCapabilities(Streaming: false, PushNotifications: false),
                DefaultInputModes: new[] { "text" },
                DefaultOutputModes: new[] { "text" },
                Skills: new[]
                {
                    new AgentSkill(
                        Id: "analisar_documentos",
                        Name: "Validacao Fiscal DIMP V10",
                        Description: "Analisa arquivos DIMP e valida estrutura, totalizadores e regras de conformidade tributaria.",
                        Tags: new[] { "infis", "fiscal", "dimp", "sofia" }
                    )
                }
            );

            return Results.Ok(card);
        })
        .WithName("GetAgentCard")
        .RequireCors("SofiaBrowser")
        .AllowAnonymous();

        return endpoints;
    }
}
