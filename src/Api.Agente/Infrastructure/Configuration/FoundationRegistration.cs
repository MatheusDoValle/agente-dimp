using Api.Agente.Application;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Lifecycle;
using Api.Agente.Application.Privacy;
using Api.Agente.Application.Reports;
using Api.Agente.Infrastructure.Jobs;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Reports;
using Api.Agente.Infrastructure.TemporaryStorage;
using Api.Agente.Infrastructure.Usage;
using Api.Agente.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Api.Agente.Infrastructure.Configuration;

public static class FoundationRegistration
{
    public static IServiceCollection AddAgentFoundation(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IValidateOptions<ProcessingOptions>>(new ProcessingOptionsValidator(configuration));
        services.AddOptions<ProcessingOptions>().Bind(configuration.GetSection("Processing")).ValidateOnStart();
        services.AddOptions<OperatorContextOptions>().Bind(configuration.GetSection("ExecutionContext"))
            .Validate(value => value.IsValid(), "Identificacao tecnica local obrigatoria.").ValidateOnStart();
        var configuredRoot = configuration["TemporaryStorage:RootPath"];
        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(contentRoot, ".local", "temporary") : Path.Combine(contentRoot, configuredRoot));
        if (root.StartsWith(Path.Combine(contentRoot, "wwwroot"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Temporarios nao podem ficar no webroot.");
        services.AddSingleton(new TemporaryStorageOptions { RootPath = root });
        services.AddSingleton<ContentSanitizer>();
        services.AddSingleton<ITemporaryArtifactStore, TemporaryArtifactStore>();
        services.AddSingleton<IJobStore, PostgresJobStore>();

        var mongoConn = configuration.GetConnectionString("MongoDb");
        var memoryEnabled = configuration.GetValue<bool>("Memory:Enabled", false);
        if (memoryEnabled || !string.IsNullOrWhiteSpace(mongoConn))
        {
            var effectiveConn = string.IsNullOrWhiteSpace(mongoConn) ? "mongodb://127.0.0.1:57017" : mongoConn;
            var databaseName = configuration["MongoDb:DatabaseName"] ?? "agente";
            services.AddSingleton<MongoDB.Driver.IMongoClient>(new MongoDB.Driver.MongoClient(effectiveConn));
            services.AddScoped(sp => sp.GetRequiredService<MongoDB.Driver.IMongoClient>().GetDatabase(databaseName));
            services.AddScoped<Api.Agente.Infrastructure.Memory.IMongoConversationContextStore, Api.Agente.Infrastructure.Memory.MongoConversationContextStore>();
            services.AddScoped<IExecutionMemoryCleanup, Api.Agente.Infrastructure.Memory.MongoExecutionMemoryCleanup>();
        }
        else
        {
            services.AddScoped<IExecutionMemoryCleanup, Api.Agente.Infrastructure.Memory.MongoExecutionMemoryCleanup>();
        }

        services.AddScoped<ExecutionFinalizer>();
        services.AddOptions<MemoryBootstrapOptions>().Bind(configuration.GetSection("Memory"));

        services.AddHttpClient<Api.Agente.Infrastructure.Rag.IRagMcpClient, Api.Agente.Infrastructure.Rag.RagMcpClient>(client =>
        {
            var baseAddress = configuration["Services:RagApi:BaseUrl"] ?? "http://127.0.0.1:5081";
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<Api.Agente.Application.AI.AnalysisContextBuilder>();
        services.AddSingleton<Api.Agente.Application.AI.AnalyticResponseValidator>();
        services.AddScoped<Api.Agente.Infrastructure.AI.IOpenAiAnalysisClient, Api.Agente.Infrastructure.AI.OpenAiAnalysisClient>();
        services.AddScoped<Api.Agente.Application.AI.IContextualAnalysisStage, Api.Agente.Application.AI.ContextualAnalysisStage>();
        services.AddScoped<Api.Agente.Application.ExecutionContext>();
        services.AddDbContext<AgentDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("AgentDb");
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Connection string do banco proprietario nao configurada.");
            options.UseNpgsql(connection);
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });
        services.AddScoped<ProviderCallAttemptStore>();
        services.AddScoped<IPriceBookCostEstimator, PriceBookCostEstimator>();
        services.AddScoped<ProviderAttemptScope>();
        services.AddScoped<Api.Agente.Application.Usage.IExecutionUsageAggregator, Api.Agente.Application.Usage.ExecutionUsageAggregator>();
        services.AddScoped<ReportExpirationService>();
        services.AddScoped<IReportStore, ReportStore>();
        services.AddScoped<BuildExecutionReport>();
        services.AddScoped<SubmitAnalysisService>();
        services.AddHostedService<AnalysisWorker>();
        services.AddHostedService<RetentionWorker>();
        services.AddHostedService<UsageRetentionWorker>();
        services.AddHostedService<ArtifactRecoveryWorker>();
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = ContractJson.Options.PropertyNamingPolicy;
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        });
        return services;
    }
}

public sealed class OperatorContextOptions
{
    public string OperatorId { get; set; } = "";
    public string ContextId { get; set; } = "";
    public string IdentityAssurance { get; set; } = "";
    public bool IsValid()
    {
        try
        {
            Domain.Execution.RequireLocalAlias(OperatorId);
            Domain.Execution.RequireLocalAlias(ContextId);
            return IdentityAssurance == "unverified_local_configuration";
        }
        catch (ArgumentException) { return false; }
    }
}

public sealed class MemoryBootstrapOptions
{
    public bool Enabled { get; set; }
}
