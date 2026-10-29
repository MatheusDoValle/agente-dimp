namespace Api.RAG.Endpoints;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new
        {
            service = "Api.RAG",
            status = "alive",
            profile = "LocalDeterministic",
            capability = "bootstrap_only"
        }));
        return endpoints;
    }
}
