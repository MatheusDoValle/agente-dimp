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
            options.UseNpgsql(connection, npgsqlOptions => npgsqlOptions.UseVector());
            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(false);
        });
        services.AddScoped<ProviderCallAttemptStore>();
        services.AddScoped<ProviderAttemptScope>();
        services.AddHostedService<Api.RAG.Workers.UsageRetentionWorker>();
        services.AddSingleton<Api.RAG.Infrastructure.Ingestion.PdfDocumentExtractor>();
        services.AddSingleton<Api.RAG.Application.Ingestion.DocumentChunker>();
        services.AddScoped<Api.RAG.Infrastructure.AI.IEmbeddingService, Api.RAG.Infrastructure.AI.EmbeddingService>();
        services.AddScoped<Api.RAG.Application.Ingestion.DocumentIngestionService>();
        services.AddScoped<Api.RAG.Application.Ingestion.IngestionQueue>();
        services.AddHostedService<Api.RAG.Workers.DocumentIngestionWorker>();
        services.AddScoped<Api.RAG.Infrastructure.Retrieval.ExactVectorSearch>();
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
