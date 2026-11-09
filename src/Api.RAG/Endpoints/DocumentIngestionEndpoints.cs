using Api.RAG.Application.Contracts;
using Api.RAG.Application.Ingestion;
using Api.RAG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Endpoints;

public static class DocumentIngestionEndpoints
{
    public static IEndpointRouteBuilder MapDocumentIngestionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documentos");

        group.MapPost("/ingestoes", async (
            HttpRequest request,
            [FromServices] DocumentIngestionService ingestionService,
            [FromServices] IConfiguration config,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_CONTENT_TYPE",
                    Message = "Esperado multipart/form-data.",
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file == null || file.Length == 0)
            {
                return Results.Json(new Problem
                {
                    Code = "FILE_MISSING",
                    Message = "Arquivo 'file' e obrigatorio.",
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            var sourceUrl = form["sourceUrl"].ToString();
            var layoutVersion = form["layoutVersion"].ToString();
            var expectedSha256 = form["expectedSha256"].ToString();
            var extractionProfileId = form["extractionProfileId"].ToString();
            var chunkProfileId = form["chunkProfileId"].ToString();
            var embeddingProfileId = form["embeddingProfileId"].ToString();

            if (string.IsNullOrWhiteSpace(sourceUrl) ||
                string.IsNullOrWhiteSpace(layoutVersion) ||
                string.IsNullOrWhiteSpace(expectedSha256) ||
                string.IsNullOrWhiteSpace(extractionProfileId) ||
                string.IsNullOrWhiteSpace(chunkProfileId) ||
                string.IsNullOrWhiteSpace(embeddingProfileId))
            {
                return Results.Json(new Problem
                {
                    Code = "METADATA_MISSING",
                    Message = "Metadados obrigatorios ausentes.",
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out _))
            {
                return Results.Json(new Problem
                {
                    Code = "INVALID_SOURCE_URL",
                    Message = "sourceUrl deve ser uma URL absoluta valida.",
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var storageDir = config["Storage:KnowledgePath"] ?? Path.Combine(Path.GetTempPath(), "AgenteDIMP.Knowledge");
                var result = await ingestionService.SubmitAsync(
                    stream,
                    sourceUrl,
                    layoutVersion,
                    expectedSha256,
                    extractionProfileId,
                    chunkProfileId,
                    embeddingProfileId,
                    storageDir,
                    ct);

                var statusCode = result.IsAlreadyCompleted ? StatusCodes.Status200OK : StatusCodes.Status202Accepted;
                return Results.Json(result.Accepted, statusCode: statusCode);
            }
            catch (UnsupportedMediaTypeException ex)
            {
                return Results.Json(new Problem
                {
                    Code = "UNSUPPORTED_MEDIA_TYPE",
                    Message = ex.Message,
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status415UnsupportedMediaType);
            }
            catch (IngestionValidationException ex)
            {
                return Results.Json(new Problem
                {
                    Code = ex.Code,
                    Message = ex.Message,
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            catch (BadHttpRequestException ex)
            {
                return Results.Json(new Problem
                {
                    Code = "BAD_REQUEST",
                    Message = ex.Message,
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status400BadRequest);
            }
        }).DisableAntiforgery();

        group.MapGet("/ingestoes/{ingestionId:guid}", async (
            Guid ingestionId,
            [FromServices] KnowledgeDbContext db,
            CancellationToken ct) =>
        {
            var job = await db.IngestionJobs.AsNoTracking().FirstOrDefaultAsync(j => j.IngestionId == ingestionId, ct);
            if (job == null)
            {
                return Results.Json(new Problem
                {
                    Code = "INGESTION_NOT_FOUND",
                    Message = $"Ingestao '{ingestionId}' nao encontrada.",
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status404NotFound);
            }

            var statusValue = job.Status switch
            {
                "em_processamento" => IngestionStatusStatusValue.EmProcessamento,
                "concluida" => IngestionStatusStatusValue.Concluida,
                "falhou" => IngestionStatusStatusValue.Falhou,
                _ => IngestionStatusStatusValue.Recebida
            };

            var stageValue = job.Stage switch
            {
                "extracao" => IngestionStatusStageValue.Extracao,
                "segmentacao" => IngestionStatusStageValue.Segmentacao,
                "embeddings" => IngestionStatusStageValue.Embeddings,
                "publicacao" => IngestionStatusStageValue.Publicacao,
                _ => IngestionStatusStageValue.Recebimento
            };

            var dto = new IngestionStatus
            {
                IngestionId = job.IngestionId,
                DocumentId = job.DocumentId,
                RevisionSha256 = job.RevisionSha256,
                Status = statusValue,
                Stage = stageValue,
                UpdatedAt = job.UpdatedAt,
                ExtractionProfileId = job.ExtractionProfileId,
                ChunkProfileId = job.ChunkProfileId,
                EmbeddingProfileId = job.EmbeddingProfileId,
                LimitationCodes = job.LimitationCodes
            };

            return Results.Json(dto, statusCode: StatusCodes.Status200OK);
        });

        group.MapGet("/{documentId:guid}/revisoes", async (
            Guid documentId,
            [FromServices] KnowledgeDbContext db,
            CancellationToken ct) =>
        {
            var docExists = await db.Documents.AsNoTracking().AnyAsync(d => d.Id == documentId, ct);
            if (!docExists)
            {
                return Results.Json(new Problem
                {
                    Code = "DOCUMENT_NOT_FOUND",
                    Message = $"Documento '{documentId}' nao encontrado.",
                    CorrelationId = Guid.NewGuid(),
                    ExecutionId = null
                }, statusCode: StatusCodes.Status404NotFound);
            }

            var revisions = await db.DocumentRevisions.AsNoTracking()
                .Where(r => r.DocumentId == documentId)
                .OrderBy(r => r.RetrievedAt)
                .ToListAsync(ct);

            var latestJob = await db.IngestionJobs.AsNoTracking()
                .Where(j => j.DocumentId == documentId && j.Status == "concluida")
                .OrderByDescending(j => j.UpdatedAt)
                .FirstOrDefaultAsync(ct);

            var items = revisions.Select(r => new Revision
            {
                DocumentId = r.DocumentId,
                RevisionSha256 = r.RevisionSha256,
                LayoutVersion = RevisionLayoutVersionValue.Version10,
                SourceUrl = r.SourceUrl,
                PageCount = r.PageCount,
                RetrievedAt = r.RetrievedAt,
                EmbeddingProfileId = latestJob?.EmbeddingProfileId ?? "text-embedding-3-small-1536",
                ExtractionProfileId = latestJob?.ExtractionProfileId ?? "pdfpig-v1",
                ChunkProfileId = latestJob?.ChunkProfileId ?? "chunk-layout10-section-record-v1"
            }).ToList();

            return Results.Json(new Revisions { Items = items }, statusCode: StatusCodes.Status200OK);
        });

        return endpoints;
    }
}
