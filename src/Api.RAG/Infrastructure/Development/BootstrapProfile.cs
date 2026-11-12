using Api.RAG.Infrastructure.AI;

namespace Api.RAG.Infrastructure.Development;

public static class BootstrapProfile
{
    public static void Validate(IConfiguration configuration)
    {
        var name = configuration["DevelopmentProfile:Name"];
        var provider = configuration["DevelopmentProfile:Provider"];
        var externalCalls = configuration.GetValue<bool>("DevelopmentProfile:ExternalCallsEnabled");

        if (name == "LocalDeterministic" && provider == "Fake" && !externalCalls) return;

        if (name == "LocalReal" && provider == "OpenAI" && externalCalls &&
            configuration["DIMP_TEST_MODE"] != "compose-local")
        {
            if (string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"]))
                throw new InvalidOperationException("Configure OpenAI:ApiKey ou OPENAI_API_KEY para usar LocalReal.");
            if (configuration["Processing:Model"] != EmbeddingService.OpenAiModel)
                throw new InvalidOperationException("O perfil semântico requer o modelo text-embedding-3-small.");
            return;
        }

        throw new InvalidOperationException(
            "Use LocalDeterministic com provider Fake ou habilite explicitamente as chamadas OpenAI no perfil LocalReal.");
    }
}
