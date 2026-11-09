using System.Text.Json;
using Api.Agente.Application.AI;
using Api.Agente.Application.Contracts;
using Api.Agente.Infrastructure.Usage;
using OpenAI.Chat;

namespace Api.Agente.Infrastructure.AI;

public interface IOpenAiAnalysisClient
{
    Task<AnalyticContent?> GenerateAnalysisAsync(
        BuiltAnalysisPrompt prompt,
        Guid executionId,
        CancellationToken cancellationToken = default);
}

public sealed class OpenAiAnalysisClient(
    ProviderCallAttemptStore attemptStore,
    IConfiguration configuration) : IOpenAiAnalysisClient
{
    public const string DefaultBaselineModel = "gpt-6-luna";

    public async Task<AnalyticContent?> GenerateAnalysisAsync(
        BuiltAnalysisPrompt prompt,
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
        var model = configuration["OpenAI:ChatModel"] ?? DefaultBaselineModel;
        var isOffline = string.IsNullOrWhiteSpace(apiKey) || configuration["DIMP_TEST_MODE"] == "compose-local";

        var request = new ProviderCallRequest(
            LogicalCallId: Guid.NewGuid(),
            CorrelationId: Guid.NewGuid(),
            ExecutionId: executionId,
            IngestionJobId: null,
            Operation: ProviderOperation.ChatCompletion,
            Provider: isOffline ? "offline" : "openai",
            Model: isOffline ? "fake-offline-chat" : model,
            AttemptNumber: 1,
            Reason: AttemptReason.Initial);

        return await attemptStore.ExecuteRecordedAsync(request, async (attempt, ct) =>
        {
            if (isOffline)
            {
                var simulatedExplanations = prompt.AllowedFindingIds.Take(5).Select(fid => new AnalyticContentExplanationsItem
                {
                    FindingIds = [fid.ToString()],
                    EvidenceIds = prompt.AllowedEvidenceChunkIds.Take(1).Select(e => e.ToString()).ToList(),
                    Text = "Inconsistência identificada de acordo com as regras estruturais e de negócio do manual DIMP."
                }).ToList();

                var simulatedContent = new AnalyticContent
                {
                    Summary = "Análise contextual realizada com sucesso a partir das evidências do manual oficial.",
                    Explanations = simulatedExplanations,
                    Indications = [],
                    Limitations = prompt.AllowedEvidenceChunkIds.Count == 0 ? ["NO_NORMATIVE_EVIDENCE_ATTACHED"] : [],
                    RequiresHumanReview = simulatedExplanations.Count > 0
                };

                var simulatedUsage = new UsageMeasurement(
                    InputTokens: Math.Max(10, prompt.UserPrompt.Length / 4),
                    OutputTokens: 50,
                    CachedTokens: 0,
                    EmbeddingTokens: null);

                return new ProviderResponse<AnalyticContent?>(
                    Value: simulatedContent,
                    Usage: simulatedUsage,
                    ReturnedModel: "fake-offline-chat",
                    ProviderRequestId: "offline-" + Guid.NewGuid().ToString("N"));
            }

            var client = new ChatClient(model, apiKey);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(prompt.SystemPrompt),
                new UserChatMessage(prompt.UserPrompt)
            };

            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "llm_analysis",
                    BinaryData.FromString(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", "llm-analysis.schema.json"))),
                    jsonSchemaIsStrict: true)
            };

            var completion = await client.CompleteChatAsync(messages, options, cancellationToken: ct);
            var text = completion.Value.Content[0].Text;

            AnalyticContent? parsed = null;
            if (!string.IsNullOrWhiteSpace(text))
            {
                parsed = ContractJson.Deserialize<AnalyticContent>(text);
            }

            var usage = new UsageMeasurement(
                InputTokens: completion.Value.Usage?.InputTokenCount,
                OutputTokens: completion.Value.Usage?.OutputTokenCount,
                CachedTokens: null,
                EmbeddingTokens: null);

            return new ProviderResponse<AnalyticContent?>(
                Value: parsed,
                Usage: usage,
                ReturnedModel: completion.Value.Model,
                ProviderRequestId: completion.Value.Id);
        }, cancellationToken);
    }
}
