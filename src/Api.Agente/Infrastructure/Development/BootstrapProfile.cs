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
                "Somente LocalDeterministic com provider Fake e chamadas externas desabilitadas esta disponivel. Os conectores LocalReal ainda nao foram implementados.");
        }
    }
}
