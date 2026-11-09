using Api.Agente.Application.Contracts;
using Api.Agente.Infrastructure.Reports;
using Microsoft.AspNetCore.Http;

namespace Api.Agente.Endpoints;

public static class GetAnalysisReportEndpoint
{
    public static void MapGetAnalysisReport(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/v1/analises/{executionId:guid}/relatorio", async (
            Guid executionId,
            IReportStore reportStore,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var correlationId = Guid.NewGuid();
            var now = clock.GetUtcNow();

            try
            {
                var report = await reportStore.GetReportAsync(executionId, now, cancellationToken);
                return Results.Ok(report);
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
