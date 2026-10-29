using Api.RAG.Endpoints;
using Api.RAG.Infrastructure.Development;

namespace Api.RAG;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        BootstrapProfile.Validate(builder.Configuration);
        builder.Services.AddSingleton<OfflineEmbeddingProvider>();

        var app = builder.Build();
        app.MapBootstrapEndpoints();
        app.Run();
    }
}
