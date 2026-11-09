using System.ComponentModel;
using System.Text.Json;
using Api.Agente.Infrastructure.Rag;
using Microsoft.SemanticKernel;

namespace Api.Agente.Infrastructure.AI;

public sealed class ManualRetrievalFunctions(
    IRagMcpClient mcpClient,
    string layoutVersion,
    string revisionSha256,
    string embeddingProfileId)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    [KernelFunction("search_manual")]
    [Description("Pesquisa no manual oficial da DIMP por trechos, regras e definicoes de campos.")]
    public async Task<string> SearchManualAsync(
        [Description("Termo de pesquisa ou pergunta sobre a regra do manual DIMP.")] string query,
        [Description("Codigo do registro DIMP de 4 digitos a filtrar, se aplicavel (ex: '0000', '0100', '1100').")] string? recordCode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "{\"error\":\"Query vazia\"}";
        }

        var recordCodes = string.IsNullOrWhiteSpace(recordCode) ? null : new[] { recordCode.Trim() };

        var searchResult = await mcpClient.SearchManualAsync(
            query: query.Trim(),
            layoutVersion: layoutVersion,
            revisionSha256: revisionSha256,
            embeddingProfileId: embeddingProfileId,
            recordCodes: recordCodes,
            cancellationToken: cancellationToken);

        if (searchResult.Hits.Count == 0)
        {
            return "{\"hits\":[],\"message\":\"Nenhuma evidencia normativa encontrada no manual oficial para a consulta.\"}";
        }

        var simplifiedHits = searchResult.Hits.Select(h => new
        {
            chunkId = h.ChunkId,
            pages = h.Pages,
            section = h.Section,
            recordCodes = h.RecordCodes,
            fieldCodes = h.FieldCodes,
            text = h.Text
        });

        return JsonSerializer.Serialize(new { hits = simplifiedHits }, JsonOpts);
    }

    [KernelFunction("get_manual_chunk")]
    [Description("Recupera o conteudo completo de um trecho especifico do manual a partir do seu identificador.")]
    public async Task<string> GetManualChunkAsync(
        [Description("Identificador UUID do chunk/trecho a recuperar.")] string chunkId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(chunkId, out var id))
        {
            return "{\"error\":\"chunkId invalido\"}";
        }

        var result = await mcpClient.GetManualChunkAsync(id, revisionSha256, cancellationToken);
        if (result == null)
        {
            return "{\"error\":\"Trecho nao encontrado na revisao oficial\"}";
        }

        return JsonSerializer.Serialize(new
        {
            chunkId = result.ChunkId,
            pages = result.Pages,
            section = result.Section,
            recordCodes = result.RecordCodes,
            fieldCodes = result.FieldCodes,
            text = result.Text
        }, JsonOpts);
    }
}
