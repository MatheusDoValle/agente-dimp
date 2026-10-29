using Api.RAG.Application.Contracts;
using Api.RAG.Infrastructure.Persistence;
using Api.RAG.Infrastructure.Usage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;


namespace Api.RAG.Infrastructure.Configuration;

public static class FoundationRegistration
{
    public static IServiceCollection AddRagFoundation(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IValidateOptions<RagOptions>>(new RagOptionsValidator(configuration));
        services.AddOptions<RagOptions>().Bind(configuration.GetSection("Processing")).ValidateOnStart();

        services.AddDbContext<KnowledgeDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("KnowledgeDb");
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
