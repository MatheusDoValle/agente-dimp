namespace Api.Agente.Endpoints;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new
        {
            service = "Api.Agente",
            status = "alive",
            profile = "LocalDeterministic",
            capability = "bootstrap_only"
        }));
        return endpoints;
    }
}
