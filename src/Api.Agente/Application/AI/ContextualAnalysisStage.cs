using Api.Agente.Application.Contracts;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.AI;
using Api.Agente.Infrastructure.Memory;
using Api.Agente.Infrastructure.Rag;
using Microsoft.Extensions.Logging;

namespace Api.Agente.Application.AI;

public interface IContextualAnalysisStage
{
    Task<AnalyticContent?> ExecuteAsync(
        Guid executionId,
        string layoutVersion,
        string manualRevisionSha256,
        string embeddingProfileId,
        IReadOnlyList<Domain.Validation.Finding> findings,
        bool systemRequiresHumanReview,
        CancellationToken cancellationToken = default);
}

public sealed class ContextualAnalysisStage(
    IRagMcpClient mcpClient,
    AnalysisContextBuilder contextBuilder,
    IOpenAiAnalysisClient aiClient,
    AnalyticResponseValidator validator,
    IMongoConversationContextStore? mongoStore,
    ILogger<ContextualAnalysisStage> logger) : IContextualAnalysisStage
{
    public async Task<AnalyticContent?> ExecuteAsync(
        Guid executionId,
        string layoutVersion,
        string manualRevisionSha256,
        string embeddingProfileId,
        IReadOnlyList<Domain.Validation.Finding> findings,
        bool systemRequiresHumanReview,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // 1. Recupera evidencias via MCP para os registros com achados determinísticos
            var evidenceHits = new List<McpHit>();
            var recordCodes = findings
                .Select(f => f.RuleId)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r =>
                {
                    var parts = r!.Split(['-', '_', ':']);
                    return parts.FirstOrDefault(p => p.Length == 4 && p.All(char.IsDigit));
                })
                .Where(c => c != null)
                .Cast<string>()
                .Distinct()
                .Take(10)
                .ToList();

            if (recordCodes.Count > 0)
            {
                var query = $"Regras e validacoes para registros DIMP: {string.Join(", ", recordCodes)}";
                var mcpResult = await mcpClient.SearchManualAsync(
                    query: query,
                    layoutVersion: layoutVersion,
                    revisionSha256: manualRevisionSha256,
                    embeddingProfileId: embeddingProfileId,
                    recordCodes: recordCodes,
                    cancellationToken: cancellationToken);

                evidenceHits.AddRange(mcpResult.Hits);
            }

            // 2. Constroi os prompts de analise
            var prompt = contextBuilder.BuildPrompt(
                executionId,
                layoutVersion,
                manualRevisionSha256,
                findings,
                evidenceHits);

            // 3. Registra contexto no MongoDB de forma assincrona e segura
            if (mongoStore != null)
            {
                try
                {
                    var contextDoc = new ConversationContextDocument
                    {
                        ExecutionId = executionId,
                        ConversationId = Guid.NewGuid(),
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
                        Messages =
                        [
                            new ContextMessage("system", prompt.SystemPrompt, DateTimeOffset.UtcNow),
                            new ContextMessage("user", prompt.UserPrompt, DateTimeOffset.UtcNow)
                        ]
                    };
                    await mongoStore.SaveContextAsync(contextDoc, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao gravar contexto inicial no MongoDB; analise contextual prosseguira.");
                }
            }

            // 4. Invoca geracao de IA
            var rawContent = await aiClient.GenerateAnalysisAsync(prompt, executionId, cancellationToken);
            if (rawContent == null)
            {
                logger.LogWarning("LLM retornou resposta nula para execucao {ExecutionId}", executionId);
                return null;
            }

            // 5. Valida a saida semantica
            var allowedFindingIds = prompt.AllowedFindingIds.ToHashSet();
            var allowedEvidenceIds = prompt.AllowedEvidenceChunkIds.ToHashSet();

            var validation = validator.Validate(rawContent, allowedFindingIds, allowedEvidenceIds, systemRequiresHumanReview);

            // 6. Atualiza contexto no MongoDB com a resposta final
            if (mongoStore != null && validation.SanitizedContent != null)
            {
                try
                {
                    var activeDoc = await mongoStore.GetActiveContextAsync(executionId, cancellationToken);
                    if (activeDoc != null)
                    {
                        activeDoc.Messages.Add(new ContextMessage("assistant", validation.SanitizedContent.Summary, DateTimeOffset.UtcNow));
                        await mongoStore.SaveContextAsync(activeDoc, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Falha ao atualizar contexto no MongoDB.");
                }
            }

            return validation.SanitizedContent;
        }
        catch (Exception ex)
        {
            // Degradação controlada: falha técnica na IA ou no RAG jamais anula os achados determinísticos
            logger.LogError(ex, "Erro durante estagio de analise contextual para execucao {ExecutionId}. Relatorio prosseguira deterministico.", executionId);
            return null;
        }
    }
}
