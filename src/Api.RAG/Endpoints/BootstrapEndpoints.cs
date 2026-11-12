namespace Api.RAG.Endpoints;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var healthHandler = (IConfiguration config) => Results.Ok(new
        {
            service = "Api.RAG",
            status = "alive",
            profile = config["DevelopmentProfile:Name"],
            capability = config["DevelopmentProfile:Name"] == "HostedQuery" ? "manual_search" : "bootstrap_only"
        });

        endpoints.MapGet("/health/live", healthHandler).AllowAnonymous();
        endpoints.MapGet("/health", healthHandler).AllowAnonymous();

        return endpoints;
    }
}
