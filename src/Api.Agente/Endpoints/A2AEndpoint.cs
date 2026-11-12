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
            IA2ATaskStore taskStore,
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

            if (request.Method == A2ARpcRequest.SendMethod)
            {
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

                    taskStore.Save(response.Result);

                    return Results.Ok(response);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Erro interno durante a execucao da analise A2A");
                    return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32603, $"Internal server error: {ex.Message}"), statusCode: 500);
                }
            }

            if (request.Method == A2ARpcRequest.TasksGetMethod)
            {
                var taskId = request.Params?.Id ?? request.Params?.TaskId;
                if (string.IsNullOrWhiteSpace(taskId))
                {
                    return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, "Invalid params: Task ID is required."), statusCode: 400);
                }

                var task = taskStore.Get(taskId);
                if (task == null)
                {
                    return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, $"Task '{taskId}' not found."), statusCode: 404);
                }

                return Results.Ok(new A2ARpcResponse("2.0", request.Id, task));
            }

            if (request.Method == A2ARpcRequest.TasksCancelMethod)
            {
                var taskId = request.Params?.Id ?? request.Params?.TaskId;
                if (string.IsNullOrWhiteSpace(taskId))
                {
                    return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, "Invalid params: Task ID is required."), statusCode: 400);
                }

                var task = taskStore.Get(taskId);
                if (task == null)
                {
                    return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, $"Task '{taskId}' not found."), statusCode: 404);
                }

                return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32602, $"Task '{taskId}' is already in terminal state '{task.Status.State}' and cannot be canceled."), statusCode: 400);
            }

            return Results.Json(A2ARpcErrorResponse.Create(request.Id, -32601, $"Method '{request.Method}' not found. Expected '{A2ARpcRequest.SendMethod}', '{A2ARpcRequest.TasksGetMethod}', or '{A2ARpcRequest.TasksCancelMethod}'."), statusCode: 404);
        })
        .WithName("A2ARpc")
        .WithTags("SOFIA A2A JSON-RPC")
        .WithSummary("Executar chamada A2A (JSON-RPC 2.0)")
        .WithDescription("Processa chamadas RPC do protocolo A2A 0.3.0 (message/send, tasks/get, tasks/cancel). Requer cabeçalho Authorization: Bearer <token>.")
        .Produces<A2ARpcResponse>(StatusCodes.Status200OK)
        .Produces<A2ARpcErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<A2ARpcErrorResponse>(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireCors("SofiaBrowser");

        return endpoints;
    }
}
