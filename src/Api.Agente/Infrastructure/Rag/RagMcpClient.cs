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
    McpRankingMetric RankingMetric, string? MatchType = null);

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

        try
        {
            if (content["error"] is JsonObject error)
            {
                var code = error["code"]?.GetValue<string>() ?? "MCP_ERROR";
                return new McpSearchResult(Guid.NewGuid(), [], [code], []);
            }

            var result = content.Deserialize<McpSearchResult>(JsonOpts);
            if (result is null || result.RetrievalId == Guid.Empty || result.Hits is null ||
                result.LimitationCodes is null || result.UsageReferences is null ||
                result.Hits.Any(hit => hit is null || hit.ChunkId == Guid.Empty || hit.DocumentId == Guid.Empty ||
                    !string.Equals(hit.DocumentRevisionSha256, revisionSha256, StringComparison.OrdinalIgnoreCase) ||
                    hit.LayoutVersion != layoutVersion || hit.EmbeddingProfileId != embeddingProfileId ||
                    hit.Pages is null || hit.RecordCodes is null || hit.FieldCodes is null ||
                    string.IsNullOrWhiteSpace(hit.Text) || hit.RankingMetric is null))
            {
                return new McpSearchResult(Guid.NewGuid(), [], ["MCP_INVALID_RESPONSE"], []);
            }

            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new McpSearchResult(Guid.NewGuid(), [], ["MCP_INVALID_RESPONSE"], []);
        }
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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
