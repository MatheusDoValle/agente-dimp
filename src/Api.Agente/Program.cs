using Api.Agente.Application.Sofia;
using Api.Agente.Endpoints;
using Api.Agente.Infrastructure.Configuration;
using Api.Agente.Infrastructure.Development;
using Api.Agente.Infrastructure.Observability;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi;

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

        var demoTokens = ClassroomDemo.Configure(builder);

        builder.Services.AddSingleton<IA2ATaskStore, A2ATaskStore>();

        if (demoTokens is null)
        {
            builder.Services.AddAgentFoundation(builder.Configuration, builder.Environment.ContentRootPath);
            builder.Services.AddSingleton<OfflineAnalysisProvider>();
        }
        else
        {
            builder.Services.AddSofiaSupport(builder.Configuration);
        }

        builder.Services.AddSingleton(SofiaDimpQuestions.Load());
        builder.Services.Configure<FormOptions>(options =>
            options.MultipartBodyLengthLimit = LocalDimpDemoEndpoint.MaxUploadBytes);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Agente Fiscal DIMP INFIS - SOFIA A2A",
                Version = "1.0.0",
                Description = "Agente Especialista DIMP (Ato COTEPE/ICMS 65/2018) integrado à SOFIA via protocolo A2A 0.3.0 com autenticação Bearer compartilhada."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "Token",
                In = ParameterLocation.Header,
                Description = "Informe a chave Bearer do treinamento SOFIA."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        var app = builder.Build();
        app.UseRedactedTelemetry();
        app.UseCors("SofiaBrowser");

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Agente DIMP A2A v1.0.0");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Agente Fiscal DIMP INFIS - Swagger UI";
        });

        app.MapBootstrapEndpoints(demoTokens is not null);
        app.MapAgentCardEndpoint();
        app.MapA2AEndpoint();

        if (demoTokens is null)
        {
            app.MapSubmitAnalysis();
            app.MapGetAnalysisStatus();
            app.MapGetAnalysisReport();
            app.MapGetAnalysisFindings();
        }
        else
        {
            ClassroomDemo.RequireToken(app, demoTokens, LocalDimpDemoEndpoint.Route);
            app.MapLocalDimpDemo(preview: app.Environment.IsProduction());
        }

        app.Run();
    }
}
