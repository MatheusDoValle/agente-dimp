using Api.Agente.Endpoints;
using Api.Agente.Infrastructure.Development;

namespace Api.Agente;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        BootstrapProfile.Validate(builder.Configuration);
        builder.Services.AddSingleton<OfflineAnalysisProvider>();

        var app = builder.Build();
        app.MapBootstrapEndpoints();
        app.Run();
    }
}
