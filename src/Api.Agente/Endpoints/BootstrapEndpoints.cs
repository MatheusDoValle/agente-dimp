namespace Api.Agente.Endpoints;

public static class BootstrapEndpoints
{
    public static IEndpointRouteBuilder MapBootstrapEndpoints(this IEndpointRouteBuilder endpoints, bool classroomDemo = false)
    {
        var healthHandler = () => Results.Ok(new
        {
            service = "Api.Agente",
            status = "alive",
            profile = classroomDemo ? "ClassroomDemo" : "LocalDeterministic",
            capability = classroomDemo ? "deterministic_a2a" : "bootstrap_only"
        });

        endpoints.MapGet("/health/live", healthHandler)
            .WithName("HealthLive")
            .WithTags("Monitoramento")
            .WithSummary("Health check liveness")
            .AllowAnonymous();

        endpoints.MapGet("/health", healthHandler)
            .WithName("Health")
            .WithTags("Monitoramento")
            .WithSummary("Health check")
            .AllowAnonymous();

        return endpoints;
    }
}
