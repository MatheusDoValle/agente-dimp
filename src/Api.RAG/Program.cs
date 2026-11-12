using Api.RAG.Endpoints;
using Api.RAG.Infrastructure.Configuration;
using Api.RAG.Infrastructure.Development;
using Api.RAG.Infrastructure.Mcp;
using Api.RAG.Infrastructure.Observability;

namespace Api.RAG;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (builder.Environment.IsEnvironment("LocalReal"))
            builder.Configuration.AddUserSecrets<Program>(optional: true).AddEnvironmentVariables().AddCommandLine(args);

        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(port))
        {
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        }
        RedactedTelemetry.ConfigureLogging(builder.Logging);
        BootstrapProfile.Validate(builder.Configuration);
        builder.Services.AddRagFoundation(builder.Configuration, builder.Environment.ContentRootPath);
        builder.Services.AddSingleton<OfflineEmbeddingProvider>();

        var app = builder.Build();
        app.UseRedactedTelemetry();
        app.MapBootstrapEndpoints();
        app.MapDocumentIngestionEndpoints();
        app.MapMcpEndpoint();
        app.Run();
    }
}
