using Api.Agente.Application;
using Api.Agente.Application.Contracts;
using Api.Agente.Domain;
using Api.Agente.Infrastructure.TemporaryStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Api.Agente.Endpoints;

public static class SubmitAnalysisEndpoint
{
    public static void MapSubmitAnalysis(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/v1/analises", async (
            HttpRequest request,
            SubmitAnalysisService service,
            CancellationToken cancellationToken) =>
        {
            var correlationId = Guid.NewGuid();

            if (request.ContentType is null || !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new Problem
                {
                    Code = "UNSUPPORTED_MEDIA_TYPE",
                    Message = "Tipo de requisicao nao suportado.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaTypeHeader) || mediaTypeHeader.Parameters is null)
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_MULTIPART",
                    Message = "Multipart ausente, invalido ou com mais de um arquivo.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var boundaryParam = mediaTypeHeader.Parameters.FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase));
            if (boundaryParam is null || !boundaryParam.Value.HasValue || string.IsNullOrWhiteSpace(boundaryParam.Value.Value))
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_MULTIPART",
                    Message = "Multipart ausente, invalido ou com mais de um arquivo.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var boundary = HeaderUtilities.RemoveQuotes(boundaryParam.Value).Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(boundary))
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_MULTIPART",
                    Message = "Multipart ausente, invalido ou com mais de um arquivo.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var reader = new MultipartReader(boundary, request.Body);

            Execution? execution = null;
            TemporaryArtifact? artifact = null;
            int fileCount = 0;

            try
            {
                MultipartSection? section;
                while ((section = await reader.ReadNextSectionAsync(cancellationToken)) != null)
                {
                    if (ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var cd)
                        && cd is not null && cd.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                    {
                        var name = cd.Name.HasValue ? HeaderUtilities.RemoveQuotes(cd.Name).Value ?? string.Empty : string.Empty;
                        var fileName = cd.FileName.HasValue ? HeaderUtilities.RemoveQuotes(cd.FileName).Value ?? string.Empty : string.Empty;

                        if (name.Equals("file", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(fileName))
                        {
                            fileCount++;
                            if (fileCount > 1)
                            {
                                if (execution is not null) await service.CleanupAsync(execution.Id);
                                return Results.Json(new Problem
                                {
                                    Code = "INVALID_MULTIPART",
                                    Message = "Multipart ausente, invalido ou com mais de um arquivo.",
                                    CorrelationId = correlationId,
                                    ExecutionId = null
                                }, statusCode: StatusCodes.Status400BadRequest);
                            }

                            execution = service.CreateExecution();
                            artifact = await service.SaveOriginalAsync(execution.Id, section.Body, cancellationToken);
                        }
                    }
                }
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                if (execution is not null) await service.CleanupAsync(execution.Id);
                return Results.Json(new Problem
                {
                    Code = "PAYLOAD_TOO_LARGE",
                    Message = "Limite efetivo do ambiente excedido; nao representa teto de produto aprovado.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status413PayloadTooLarge);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException)
            {
                if (execution is not null) await service.CleanupAsync(execution.Id);
                return Results.Json(new Problem
                {
                    Code = "INVALID_MULTIPART",
                    Message = "Multipart ausente, invalido ou com mais de um arquivo.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            if (execution is null || artifact is null)
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_MULTIPART",
                    Message = "Multipart ausente, invalido ou com mais de um arquivo.",
                    CorrelationId = correlationId,
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await service.CompleteSubmissionAsync(execution, artifact, correlationId, cancellationToken);
            if (result.IsSuccess && result.Accepted is not null)
            {
                return Results.Accepted(result.Accepted.StatusUrl, result.Accepted);
            }

            return Results.Json(result.Problem, statusCode: result.StatusCode);
        });
    }
}
