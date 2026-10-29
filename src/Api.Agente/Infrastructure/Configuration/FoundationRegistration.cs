using Api.Agente.Application.Contracts;
using Api.Agente.Infrastructure.Persistence;
using Api.Agente.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using Api.Agente.Application.Privacy;
using Api.Agente.Application.Lifecycle;
using Api.Agente.Infrastructure.TemporaryStorage;

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
        services.AddScoped<IExecutionMemoryCleanup, DisabledExecutionMemoryCleanup>();
        services.AddScoped<ExecutionFinalizer>();
        services.AddOptions<MemoryBootstrapOptions>().Bind(configuration.GetSection("Memory"))
            .Validate(value => !value.Enabled, "Ativar memoria exige o adaptador MongoDB da US3.").ValidateOnStart();
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
