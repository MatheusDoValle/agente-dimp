using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Api.RAG.Application.Contracts;
using Api.RAG.Infrastructure.Persistence;
using Api.RAG.Infrastructure.Retrieval;
using Api.RAG.Infrastructure.Usage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Infrastructure.Mcp;

public static class ManualTools
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IEndpointRouteBuilder MapMcpEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/mcp", async (
            HttpRequest request,
            [FromServices] KnowledgeDbContext db,
            [FromServices] ExactVectorSearch vectorSearch,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);

            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.BadRequest(new { jsonrpc = "2.0", id = (object?)null, error = new { code = -32700, message = "Parse error" } });
            }

            JsonNode? rootNode;
            try
            {
                rootNode = JsonNode.Parse(body);
            }
            catch
            {
                return Results.BadRequest(new { jsonrpc = "2.0", id = (object?)null, error = new { code = -32700, message = "Parse error" } });
            }

            if (rootNode is not JsonObject reqObj)
            {
                return Results.BadRequest(new { jsonrpc = "2.0", id = (object?)null, error = new { code = -32600, message = "Invalid Request" } });
            }

            var id = reqObj["id"]?.DeepClone();
            var method = reqObj["method"]?.GetValue<string>();

            switch (method)
            {
                case "initialize":
                    return Results.Ok(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            protocolVersion = "2026-07-28",
                            capabilities = new { tools = new { listChanged = false } },
                            serverInfo = new { name = "Api.RAG", version = "1.0.0" }
                        }
                    });

                case "tools/list":
                    return Results.Ok(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            tools = GetToolDefinitions()
                        }
                    });

                case "tools/call":
                    var paramsNode = reqObj["params"] as JsonObject;
                    var toolName = paramsNode?["name"]?.GetValue<string>();
                    var arguments = paramsNode?["arguments"] as JsonObject ?? [];

                    var callResult = await ExecuteToolCallAsync(toolName, arguments, db, vectorSearch, ct);
                    return Results.Ok(new
                    {
                        jsonrpc = "2.0",
                        id,
                        result = new
                        {
                            content = new[]
                            {
                                new
                                {
                                    type = "text",
                                    text = JsonSerializer.Serialize(callResult, JsonOpts)
                                }
                            }
                        }
                    });

                default:
                    return Results.Ok(new
                    {
                        jsonrpc = "2.0",
                        id,
                        error = new { code = -32601, message = $"Method '{method}' not found" }
                    });
            }
        });

        return endpoints;
    }

    private static object[] GetToolDefinitions() =>
    [
        new
        {
            name = "search_manual",
            description = "Recupera trechos do manual DIMP oficial com busca exata filtrada.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string" },
                    layoutVersion = new { type = "string", @const = "10" },
                    documentRevisionSha256 = new { type = "string" },
                    embeddingProfileId = new { type = "string" },
                    recordCodes = new { type = "array", items = new { type = "string" } },
                    ruleIds = new { type = "array", items = new { type = "string" } }
                },
                required = new[] { "query", "layoutVersion", "documentRevisionSha256", "embeddingProfileId" }
            }
        },
        new
        {
            name = "get_manual_chunk",
            description = "Obtém um chunk específico pelo ID e hash da revisão.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    chunkId = new { type = "string", format = "uuid" },
                    documentRevisionSha256 = new { type = "string" }
                },
                required = new[] { "chunkId", "documentRevisionSha256" }
            }
        },
        new
        {
            name = "get_document_revision",
            description = "Obtém metadados de uma revisão publicada do documento.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    documentRevisionSha256 = new { type = "string" }
                },
                required = new[] { "documentRevisionSha256" }
            }
        },
        new
        {
            name = "get_ingestion_usage",
            description = "Retorna consumo e tentativas correlacionadas a uma ingestão.",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    ingestionId = new { type = "string", format = "uuid" }
                },
                required = new[] { "ingestionId" }
            }
        }
    ];

    private static async Task<object> ExecuteToolCallAsync(
        string? toolName,
        JsonObject args,
        KnowledgeDbContext db,
        ExactVectorSearch vectorSearch,
        CancellationToken ct)
    {
        return toolName switch
        {
            "search_manual" => await SearchManualAsync(args, db, vectorSearch, ct),
            "get_manual_chunk" => await GetManualChunkAsync(args, db, ct),
            "get_document_revision" => await GetDocumentRevisionAsync(args, db, ct),
            "get_ingestion_usage" => await GetIngestionUsageAsync(args, db, ct),
            _ => CreateBusinessError("TOOL_NOT_FOUND", $"Ferramenta '{toolName}' desconhecida.", false)
        };
    }

    private static async Task<object> SearchManualAsync(
        JsonObject args,
        KnowledgeDbContext db,
        ExactVectorSearch vectorSearch,
        CancellationToken ct)
    {
        var query = args["query"]?.GetValue<string>();
        var layoutVersion = args["layoutVersion"]?.GetValue<string>();
        var sha = args["documentRevisionSha256"]?.GetValue<string>();
        var profileId = args["embeddingProfileId"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(layoutVersion) ||
            string.IsNullOrWhiteSpace(sha) || string.IsNullOrWhiteSpace(profileId))
        {
            return CreateBusinessError("ARGUMENT_INVALID", "Argumentos obrigatorios ausentes.", false);
        }

        if (layoutVersion != "10")
        {
            return CreateBusinessError("LAYOUT_NOT_SUPPORTED", "LayoutVersion suportada e '10'.", false);
        }

        var normalizedSha = sha.Trim().ToLowerInvariant();
        var revExists = await db.DocumentRevisions.AsNoTracking().AnyAsync(r => r.RevisionSha256 == normalizedSha, ct);
        if (!revExists)
        {
            return CreateBusinessError("REVISION_NOT_FOUND", $"Revisao '{sha}' nao encontrada.", false);
        }

        var recordCodes = args["recordCodes"]?.AsArray().Select(n => n?.GetValue<string>()).Where(s => s != null).Cast<string>().ToArray();

        var searchResult = await vectorSearch.SearchAsync(
            query,
            layoutVersion,
            normalizedSha,
            profileId,
            recordCodes: recordCodes,
            topK: 5,
            cancellationToken: ct);

        var hits = searchResult.Hits.Select(h => new
        {
            chunkId = h.ChunkId,
            documentId = h.DocumentId,
            documentRevisionSha256 = h.DocumentRevisionSha256,
            layoutVersion = h.LayoutVersion,
            embeddingProfileId = h.EmbeddingProfileId,
            pages = h.Pages,
            section = (string?)h.Section,
            recordCodes = h.RecordCodes,
            fieldCodes = h.FieldCodes,
            text = h.Text,
            rankingMetric = new
            {
                name = "cosine_distance",
                value = Math.Max(0.0, Math.Min(2.0, h.RankingMetric))
            }
        }).ToList();

        var usageReferences = new List<object>();
        if (searchResult.AttemptId.HasValue)
        {
            var attempt = await db.ProviderCallAttempts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AttemptId == searchResult.AttemptId.Value, ct);

            if (attempt != null)
            {
                usageReferences.Add(new
                {
                    callAttemptId = attempt.AttemptId,
                    serviceOwner = "Api.RAG",
                    usageState = attempt.UsageState.ToString().ToLowerInvariant(),
                    tokensInput = attempt.TokensInput,
                    tokensOutput = attempt.TokensOutput,
                    cachedTokens = attempt.CachedTokens,
                    embeddingTokens = attempt.EmbeddingTokens
                });
            }
        }

        return new
        {
            retrievalId = searchResult.RetrievalId,
            hits,
            limitationCodes = searchResult.LimitationCodes,
            usageReferences
        };
    }

    private static async Task<object> GetManualChunkAsync(JsonObject args, KnowledgeDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(args["chunkId"]?.GetValue<string>(), out var chunkId) ||
            string.IsNullOrWhiteSpace(args["documentRevisionSha256"]?.GetValue<string>()))
        {
            return CreateBusinessError("ARGUMENT_INVALID", "chunkId e documentRevisionSha256 sao obrigatorios.", false);
        }

        var sha = args["documentRevisionSha256"]!.GetValue<string>().Trim().ToLowerInvariant();

        var published = db.PublishedVectors();
        var chunk = await db.KnowledgeChunks.AsNoTracking()
            .Include(c => c.Revision)
            .FirstOrDefaultAsync(c => c.Id == chunkId && c.Revision!.RevisionSha256 == sha &&
                published.Any(vector => vector.ChunkId == c.Id), ct);

        if (chunk == null)
        {
            return CreateBusinessError("CHUNK_NOT_FOUND", $"Chunk '{chunkId}' nao encontrado para revisao '{sha}'.", false);
        }

        return new
        {
            chunk = new
            {
                chunkId = chunk.Id,
                documentId = chunk.Revision!.DocumentId,
                documentRevisionSha256 = chunk.Revision.RevisionSha256,
                layoutVersion = chunk.Revision.LayoutVersion,
                pages = chunk.Pages,
                section = (string?)chunk.Section,
                recordCodes = string.IsNullOrWhiteSpace(chunk.RecordCode) ? Array.Empty<string>() : [chunk.RecordCode],
                fieldCodes = string.IsNullOrWhiteSpace(chunk.FieldCode) ? Array.Empty<string>() : [chunk.FieldCode],
                text = chunk.Text
            },
            limitationCodes = Array.Empty<string>()
        };
    }

    private static async Task<object> GetDocumentRevisionAsync(JsonObject args, KnowledgeDbContext db, CancellationToken ct)
    {
        var sha = args["documentRevisionSha256"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(sha))
        {
            return CreateBusinessError("ARGUMENT_INVALID", "documentRevisionSha256 e obrigatorio.", false);
        }

        var normalizedSha = sha.Trim().ToLowerInvariant();
        var revision = await db.DocumentRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RevisionSha256 == normalizedSha, ct);

        if (revision == null)
        {
            return CreateBusinessError("REVISION_NOT_FOUND", $"Revisao '{sha}' nao encontrada.", false);
        }

        var profileIds = await db.PublishedVectors().AsNoTracking()
            .Where(v => v.Chunk!.RevisionId == revision.Id)
            .Select(v => v.EmbeddingProfileId)
            .Distinct()
            .ToListAsync(ct);

        if (profileIds.Count == 0)
            return CreateBusinessError("REVISION_NOT_FOUND", "A revisão ainda não foi publicada.", false);

        return new
        {
            documentId = revision.DocumentId,
            documentRevisionSha256 = revision.RevisionSha256,
            sourceUrl = revision.SourceUrl.ToString(),
            layoutVersion = "10",
            pageCount = revision.PageCount,
            embeddingProfileIds = profileIds
        };
    }

    private static async Task<object> GetIngestionUsageAsync(JsonObject args, KnowledgeDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(args["ingestionId"]?.GetValue<string>(), out var ingestionId))
        {
            return CreateBusinessError("ARGUMENT_INVALID", "ingestionId invalido.", false);
        }

        var attempts = await db.ProviderCallAttempts.AsNoTracking()
            .Where(a => a.IngestionJobId == ingestionId)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

        var references = attempts.Select(a => new
        {
            callAttemptId = a.AttemptId,
            serviceOwner = "Api.RAG",
            usageState = a.UsageState.ToString().ToLowerInvariant(),
            tokensInput = a.TokensInput,
            tokensOutput = a.TokensOutput,
            cachedTokens = a.CachedTokens,
            embeddingTokens = a.EmbeddingTokens
        }).ToList();

        var hasUnknown = attempts.Any(a => a.UsageState == UsageState.Unknown);

        return new
        {
            ingestionId,
            usageReferences = references,
            hasUnknownUsage = hasUnknown
        };
    }

    private static object CreateBusinessError(string code, string message, bool retryable) =>
        new
        {
            error = new
            {
                code,
                message,
                retryable,
                correlationId = Guid.NewGuid()
            }
        };
}
