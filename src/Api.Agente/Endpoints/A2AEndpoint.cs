using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Sofia;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Endpoints;

public static class A2AEndpoint
{
    public static IEndpointRouteBuilder MapA2AEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/a2a", async (
            [FromBody] A2ARpcRequest request,
            ISofiaAnalysisHandler handler,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("A2AEndpoint");

            if (request == null || string.IsNullOrWhiteSpace(request.JsonRpc))
            {
                return Results.Json(A2ARpcErrorResponse.Create(null, -32600, "Invalid Request: empty payload or missing jsonrpc."), statusCode: 400);
            }

            if (request.JsonRpc != A2ARpcRequest.ExpectedVersion)
            {
                return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32600, $"Invalid JSON-RPC version '{request.JsonRpc}'. Expected '2.0'."), statusCode: 400);
            }

            if (request.Method != A2ARpcRequest.SendMethod)
            {
                return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32601, $"Method '{request.Method}' not found. Expected '{A2ARpcRequest.SendMethod}'."), statusCode: 404);
            }

            var parts = request.Params?.Message?.Parts;
            var textPart = parts?.FirstOrDefault(p => string.Equals(p.Kind, "text", StringComparison.OrdinalIgnoreCase));
            if (textPart == null || string.IsNullOrWhiteSpace(textPart.Text))
            {
                return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, "Invalid params: a TextPart with JSON payload is required."), statusCode: 400);
            }

            SolicitacaoAnaliseSofia solicitacao;
            try
            {
                solicitacao = JsonSerializer.Deserialize<SolicitacaoAnaliseSofia>(textPart.Text, ContractJson.Options)
                    ?? throw new JsonException("Payload nulo");
                solicitacao.Validate();
            }
            catch (Exception ex) when (ex is JsonException or ValidationException)
            {
                logger.LogWarning("Payload SolicitacaoAnaliseSofia invalido: {Error}", ex.Message);
                return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, $"Invalid SolicitacaoAnaliseSofia payload: {ex.Message}"), statusCode: 400);
            }

            try
            {
                var resultado = await handler.HandleAsync(solicitacao, cancellationToken);
                var resultadoJson = JsonSerializer.Serialize(resultado, ContractJson.Options);

                var taskId = $"task-{solicitacao.IdSolicitacao:N}"[..12];
                var contextId = $"ctx-{solicitacao.IdExecucao:N}"[..12];

                var response = A2ARpcResponse.Create(
                    rpcId: request.Id,
                    taskId: taskId,
                    contextId: contextId,
                    artifactName: "analise-sofia-v1",
                    artifactContent: resultadoJson
                );

                return Results.Ok(response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erro interno durante a execucao da analise A2A");
                return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32603, $"Internal server error: {ex.Message}"), statusCode: 500);
            }
        })
        .WithName("A2AMessageSend")
        .RequireCors("SofiaBrowser")
        .AllowAnonymous();

        return endpoints;
    }
}
