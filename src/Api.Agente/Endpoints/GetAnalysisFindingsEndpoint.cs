using Api.Agente.Application.Contracts;
using Api.Agente.Infrastructure.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Agente.Endpoints;

public static class GetAnalysisFindingsEndpoint
{
    public static void MapGetAnalysisFindings(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/analises/{executionId:guid}/achados", async (
            Guid executionId,
            [FromQuery] string? cursor,
            [FromQuery] int? pageSize,
            IReportStore reportStore,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var correlationId = Guid.NewGuid();
            var size = pageSize ?? 100;
            if (size < 1 || size > 500)
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_PAGESIZE",
                    Message = "O parametro pageSize deve ser entre 1 e 500.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var now = clock.GetUtcNow();

            try
            {
                var page = await reportStore.GetFindingsPageAsync(executionId, cursor, size, now, cancellationToken);
                return Results.Ok(page);
            }
            catch (InvalidCursorException ex)
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_CURSOR",
                    Message = ex.Message,
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (ExecutionNotFoundException)
            {
                return Results.Json(new Problem
                {
                    Code = "EXECUTION_NOT_FOUND",
                    Message = "Execucao nao encontrada.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status404NotFound);
            }
            catch (ReportExpiredException)
            {
                return Results.Json(new Problem
                {
                    Code = "REPORT_EXPIRED",
                    Message = "Relatorio expirado apos 30 dias.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status410Gone);
            }
            catch (AnalysisInProgressException)
            {
                return Results.Json(new Problem
                {
                    Code = "ANALYSIS_IN_PROGRESS",
                    Message = "Relatorio ainda nao disponivel.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status409Conflict);
            }
            catch (ReportUnavailableException)
            {
                return Results.Json(new Problem
                {
                    Code = "REPORT_UNAVAILABLE",
                    Message = "Relatorio indisponivel para esta execucao.",
                    CorrelationId = correlationId,
                    ExecutionId = executionId
                }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        });
    }
}
