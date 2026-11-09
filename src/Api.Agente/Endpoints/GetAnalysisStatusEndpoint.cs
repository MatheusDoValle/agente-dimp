using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Api.Agente.Endpoints;

public static class GetAnalysisStatusEndpoint
{
    public static void MapGetAnalysisStatus(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/analises/{executionId:guid}", async (
            Guid executionId,
            AgentDbContext dbContext,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var correlationId = Guid.NewGuid();

            var execution = await dbContext.Executions.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == executionId, cancellationToken);

            if (execution is null)
            {
                return Results.Json(new Problem
                {
                    Code = "EXECUTION_NOT_FOUND",
                    Message = "Execucao nao encontrada.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status404NotFound);
            }

            var now = clock.GetUtcNow();
            if (execution.ReportExpiresAt.HasValue && now >= execution.ReportExpiresAt.Value)
            {
                return Results.Json(new Problem
                {
                    Code = "EXECUTION_EXPIRED",
                    Message = "Execucao expirada apos janela de retencao.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status410Gone);
            }

            var hasReport = execution.EndedAt.HasValue
                && await dbContext.Reports.AnyAsync(r => r.ExecutionId == execution.Id, cancellationToken);
            var reportUrl = hasReport ? $"/api/v1/analises/{execution.Id}/relatorio" : null;

            var status = new ExecutionStatus
            {
                ExecutionId = execution.Id,
                Status = execution.Status switch
                {
                    ExecutionState.Received => ExecutionStatusStatusValue.Recebida,
                    ExecutionState.Processing => ExecutionStatusStatusValue.EmProcessamento,
                    ExecutionState.Completed => ExecutionStatusStatusValue.Concluida,
                    ExecutionState.PartiallyCompleted => ExecutionStatusStatusValue.ConcluidaParcialmente,
                    ExecutionState.Failed => ExecutionStatusStatusValue.Falhou,
                    ExecutionState.Cancelled => ExecutionStatusStatusValue.Cancelada,
                    _ => ExecutionStatusStatusValue.Falhou
                },
                Stage = execution.Stage switch
                {
                    ExecutionStage.Receiving => ExecutionStatusStageValue.Recebimento,
                    ExecutionStage.Identification => ExecutionStatusStageValue.Identificacao,
                    ExecutionStage.Validation => ExecutionStatusStageValue.Validacao,
                    ExecutionStage.Retrieval => ExecutionStatusStageValue.Recuperacao,
                    ExecutionStage.Analysis => ExecutionStatusStageValue.Analise,
                    ExecutionStage.Composition => ExecutionStatusStageValue.Composicao,
                    ExecutionStage.Cleanup => ExecutionStatusStageValue.Limpeza,
                    _ => ExecutionStatusStageValue.Limpeza
                },
                UpdatedAt = execution.UpdatedAt,
                EndedAt = execution.EndedAt,
                ReportAvailable = hasReport,
                ReportUrl = reportUrl,
                ReportExpiresAt = execution.ReportExpiresAt,
                LimitationCodes = execution.LimitationCodes ?? Array.Empty<string>()
            };

            return Results.Ok(status);
        });
    }
}
