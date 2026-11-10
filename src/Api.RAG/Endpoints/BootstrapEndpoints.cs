namespace Api.RAG.Endpoints;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var healthHandler = () => Results.Ok(new
        {
            service = "Api.RAG",
            status = "alive",
            profile = "LocalDeterministic",
            capability = "bootstrap_only"
        });

        endpoints.MapGet("/health/live", healthHandler).AllowAnonymous();
        endpoints.MapGet("/health", healthHandler).AllowAnonymous();

        return endpoints;
    }
}
