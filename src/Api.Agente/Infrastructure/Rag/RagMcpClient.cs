using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Agente.Infrastructure.Rag;

public sealed record McpRankingMetric(string Name, double Value);

public sealed record McpHit(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentRevisionSha256,
    string LayoutVersion,
    string EmbeddingProfileId,
    int[] Pages,
    string? Section,
    IReadOnlyList<string> RecordCodes,
    IReadOnlyList<string> FieldCodes,
    string Text,
    McpRankingMetric RankingMetric);

public sealed record McpUsageReference(
    Guid CallAttemptId,
    string ServiceOwner,
    string UsageState,
    long? TokensInput,
    long? TokensOutput,
    long? CachedTokens,
    long? EmbeddingTokens);

public sealed record McpSearchResult(
    Guid RetrievalId,
    IReadOnlyList<McpHit> Hits,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<McpUsageReference> UsageReferences);

public sealed record McpChunkResult(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentRevisionSha256,
    string LayoutVersion,
    int[] Pages,
    string? Section,
    IReadOnlyList<string> RecordCodes,
    IReadOnlyList<string> FieldCodes,
    string Text,
    IReadOnlyList<string> LimitationCodes);

public sealed record McpRevisionResult(
    Guid DocumentId,
    string DocumentRevisionSha256,
    string SourceUrl,
    string LayoutVersion,
    long PageCount,
    IReadOnlyList<string> EmbeddingProfileIds);

public sealed record McpUsageResult(
    Guid IngestionId,
    IReadOnlyList<McpUsageReference> UsageReferences,
    bool HasUnknownUsage);

public interface IRagMcpClient
{
    Task<McpSearchResult> SearchManualAsync(
        string query,
        string layoutVersion,
        string revisionSha256,
        string embeddingProfileId,
        IReadOnlyList<string>? recordCodes = null,
        CancellationToken cancellationToken = default);

    Task<McpChunkResult?> GetManualChunkAsync(
        Guid chunkId,
        string revisionSha256,
        CancellationToken cancellationToken = default);

    Task<McpRevisionResult?> GetDocumentRevisionAsync(
        string revisionSha256,
        CancellationToken cancellationToken = default);

    Task<McpUsageResult?> GetIngestionUsageAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default);
}

public sealed class RagMcpClient(HttpClient httpClient) : IRagMcpClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<McpSearchResult> SearchManualAsync(
        string query,
        string layoutVersion,
        string revisionSha256,
        string embeddingProfileId,
        IReadOnlyList<string>? recordCodes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingProfileId);

        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new
            {
                name = "search_manual",
                arguments = new
                {
                    query,
                    layoutVersion,
                    documentRevisionSha256 = revisionSha256,
                    embeddingProfileId,
                    recordCodes = recordCodes ?? []
                }
            }
        };

        var content = await CallToolRawAsync(requestBody, cancellationToken);
        if (content == null)
        {
            return new McpSearchResult(Guid.NewGuid(), [], ["MCP_UNAVAILABLE"], []);
        }

        // Verifica se retornou erro de negócio
        if (content["error"] != null)
        {
            var errorCode = content["error"]?["code"]?.GetValue<string>() ?? "MCP_ERROR";
            return new McpSearchResult(Guid.NewGuid(), [], [errorCode], []);
        }

        var retrievalId = Guid.TryParse(content["retrievalId"]?.GetValue<string>(), out var rid) ? rid : Guid.NewGuid();
        var limitationCodes = content["limitationCodes"]?.AsArray()
            .Select(n => n?.GetValue<string>()).Where(s => s != null).Cast<string>().ToList() ?? [];

        var hits = new List<McpHit>();
        if (content["hits"] is JsonArray hitsArr)
        {
            foreach (var h in hitsArr)
            {
                if (h is not JsonObject ho) continue;
                var chunkId = Guid.TryParse(ho["chunkId"]?.GetValue<string>(), out var cid) ? cid : Guid.Empty;
                var docId = Guid.TryParse(ho["documentId"]?.GetValue<string>(), out var did) ? did : Guid.Empty;
                var revSha = ho["documentRevisionSha256"]?.GetValue<string>() ?? revisionSha256;
                var layVer = ho["layoutVersion"]?.GetValue<string>() ?? layoutVersion;
                var embProf = ho["embeddingProfileId"]?.GetValue<string>() ?? embeddingProfileId;
                var pages = ho["pages"]?.AsArray().Select(n => n?.GetValue<int>() ?? 0).ToArray() ?? [];
                var section = ho["section"]?.GetValue<string>();
                var recCodes = ho["recordCodes"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? [];
                var fieldCodes = ho["fieldCodes"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? [];
                var text = ho["text"]?.GetValue<string>() ?? "";

                var metricName = ho["rankingMetric"]?["name"]?.GetValue<string>() ?? "cosine_distance";
                var metricVal = ho["rankingMetric"]?["value"]?.GetValue<double>() ?? 1.0;

                hits.Add(new McpHit(
                    chunkId, docId, revSha, layVer, embProf, pages, section,
                    recCodes, fieldCodes, text, new McpRankingMetric(metricName, metricVal)));
            }
        }

        var usageReferences = new List<McpUsageReference>();
        if (content["usageReferences"] is JsonArray usageArr)
        {
            foreach (var u in usageArr)
            {
                if (u is not JsonObject uo) continue;
                var attemptId = Guid.TryParse(uo["callAttemptId"]?.GetValue<string>(), out var aid) ? aid : Guid.Empty;
                var serviceOwner = uo["serviceOwner"]?.GetValue<string>() ?? "Api.RAG";
                var usageState = uo["usageState"]?.GetValue<string>() ?? "unknown";
                var tokensIn = uo["tokensInput"]?.GetValue<long?>();
                var tokensOut = uo["tokensOutput"]?.GetValue<long?>();
                var cached = uo["cachedTokens"]?.GetValue<long?>();
                var embTokens = uo["embeddingTokens"]?.GetValue<long?>();

                usageReferences.Add(new McpUsageReference(
                    attemptId, serviceOwner, usageState, tokensIn, tokensOut, cached, embTokens));
            }
        }

        return new McpSearchResult(retrievalId, hits, limitationCodes, usageReferences);
    }

    public async Task<McpChunkResult?> GetManualChunkAsync(Guid chunkId, string revisionSha256, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new
            {
                name = "get_manual_chunk",
                arguments = new
                {
                    chunkId,
                    documentRevisionSha256 = revisionSha256
                }
            }
        };

        var content = await CallToolRawAsync(requestBody, cancellationToken);
        if (content == null || content["chunk"] is not JsonObject chunkObj) return null;

        var cid = Guid.TryParse(chunkObj["chunkId"]?.GetValue<string>(), out var cId) ? cId : chunkId;
        var docId = Guid.TryParse(chunkObj["documentId"]?.GetValue<string>(), out var dId) ? dId : Guid.Empty;
        var revSha = chunkObj["documentRevisionSha256"]?.GetValue<string>() ?? revisionSha256;
        var layVer = chunkObj["layoutVersion"]?.GetValue<string>() ?? "10";
        var pages = chunkObj["pages"]?.AsArray().Select(n => n?.GetValue<int>() ?? 0).ToArray() ?? [];
        var section = chunkObj["section"]?.GetValue<string>();
        var recCodes = chunkObj["recordCodes"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? [];
        var fieldCodes = chunkObj["fieldCodes"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? [];
        var text = chunkObj["text"]?.GetValue<string>() ?? "";

        var limitations = content["limitationCodes"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? [];

        return new McpChunkResult(cid, docId, revSha, layVer, pages, section, recCodes, fieldCodes, text, limitations);
    }

    public async Task<McpRevisionResult?> GetDocumentRevisionAsync(string revisionSha256, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new
            {
                name = "get_document_revision",
                arguments = new
                {
                    documentRevisionSha256 = revisionSha256
                }
            }
        };

        var content = await CallToolRawAsync(requestBody, cancellationToken);
        if (content == null || content["documentId"] == null) return null;

        var docId = Guid.TryParse(content["documentId"]?.GetValue<string>(), out var dId) ? dId : Guid.Empty;
        var sourceUrl = content["sourceUrl"]?.GetValue<string>() ?? "";
        var layoutVer = content["layoutVersion"]?.GetValue<string>() ?? "10";
        var pageCount = content["pageCount"]?.GetValue<long>() ?? 0;
        var profileIds = content["embeddingProfileIds"]?.AsArray().Select(n => n?.GetValue<string>() ?? "").ToList() ?? [];

        return new McpRevisionResult(docId, revisionSha256, sourceUrl, layoutVer, pageCount, profileIds);
    }

    public async Task<McpUsageResult?> GetIngestionUsageAsync(Guid ingestionId, CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString("N"),
            method = "tools/call",
            @params = new
            {
                name = "get_ingestion_usage",
                arguments = new
                {
                    ingestionId
                }
            }
        };

        var content = await CallToolRawAsync(requestBody, cancellationToken);
        if (content == null || content["ingestionId"] == null) return null;

        var references = new List<McpUsageReference>();
        if (content["usageReferences"] is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not JsonObject uo) continue;
                var attemptId = Guid.TryParse(uo["callAttemptId"]?.GetValue<string>(), out var aid) ? aid : Guid.Empty;
                var serviceOwner = uo["serviceOwner"]?.GetValue<string>() ?? "Api.RAG";
                var usageState = uo["usageState"]?.GetValue<string>() ?? "unknown";
                var tokensIn = uo["tokensInput"]?.GetValue<long?>();
                var tokensOut = uo["tokensOutput"]?.GetValue<long?>();
                var cached = uo["cachedTokens"]?.GetValue<long?>();
                var embTokens = uo["embeddingTokens"]?.GetValue<long?>();

                references.Add(new McpUsageReference(
                    attemptId, serviceOwner, usageState, tokensIn, tokensOut, cached, embTokens));
            }
        }

        var hasUnknown = content["hasUnknownUsage"]?.GetValue<bool>() ?? false;
        return new McpUsageResult(ingestionId, references, hasUnknown);
    }

    private async Task<JsonObject?> CallToolRawAsync(object requestBody, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var respString = await response.Content.ReadAsStringAsync(ct);
            var root = JsonNode.Parse(respString) as JsonObject;
            var text = root?["result"]?["content"]?[0]?["text"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(text)) return null;

            return JsonNode.Parse(text) as JsonObject;
        }
        catch
        {
            return null;
        }
    }
}
