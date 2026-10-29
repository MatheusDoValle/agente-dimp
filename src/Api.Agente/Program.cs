using Api.Agente.Endpoints;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.Development;
using Api.Agente.Infrastructure.Observability;

namespace Api.Agente;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        RedactedTelemetry.ConfigureLogging(builder.Logging);
        BootstrapProfile.Validate(builder.Configuration);
        builder.Services.AddAgentFoundation(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddSingleton<OfflineAnalysisProvider>();

        var app = builder.Build();
        app.UseRedactedTelemetry();
        app.MapBootstrapEndpoints();
        app.Run();
    }
}
