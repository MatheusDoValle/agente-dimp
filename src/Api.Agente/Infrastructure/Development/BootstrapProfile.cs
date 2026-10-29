namespace Api.Agente.Infrastructure.Development;

public static class BootstrapProfile
{
    public static void Validate(IConfiguration configuration)
    {
        if (configuration["DevelopmentProfile:Name"] != "LocalDeterministic"
            || configuration.GetValue<bool>("DevelopmentProfile:ExternalCallsEnabled")
            || configuration["DevelopmentProfile:Provider"] != "Fake")
        {
            throw new InvalidOperationException(
                "Fase 1 permite somente LocalDeterministic com provider Fake e chamadas externas desabilitadas. LocalReal depende das fases seguintes.");
        }
    }
}
