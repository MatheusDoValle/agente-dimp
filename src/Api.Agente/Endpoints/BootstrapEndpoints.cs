namespace Api.Agente.Endpoints;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var healthHandler = () => Results.Ok(new
        {
            service = "Api.Agente",
            status = "alive",
            profile = "LocalDeterministic",
            capability = "bootstrap_only"
        });

        endpoints.MapGet("/health/live", healthHandler).AllowAnonymous();
        endpoints.MapGet("/health", healthHandler).AllowAnonymous();

        return endpoints;
    }
}
