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
        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(port))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        }
        RedactedTelemetry.ConfigureLogging(builder.Logging);
        BootstrapProfile.Validate(builder.Configuration);
        builder.Services.AddAgentFoundation(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddSingleton<OfflineAnalysisProvider>();

        var app = builder.Build();
        app.UseRedactedTelemetry();
        app.UseCors("SofiaBrowser");
        app.MapBootstrapEndpoints();
        app.MapAgentCardEndpoint();
        app.MapA2AEndpoint();
        app.MapSubmitAnalysis();
        app.MapGetAnalysisStatus();
        app.MapGetAnalysisReport();
        app.MapGetAnalysisFindings();
        app.Run();
    }
}
