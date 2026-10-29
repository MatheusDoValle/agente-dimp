using System.Diagnostics;
using System.Text.Json;
using Api.Agente.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Trace;
using Api.Agente.Application.Privacy;

namespace Api.Agente.Infrastructure.Observability;

public static class RedactedTelemetry
{
    public const string SourceName = "AgenteDIMP.Safe.Api.Agente";
    public const string CorrelationKey = "DIMP.HostCorrelationId";
    public const string ExecutionKey = "DIMP.HostExecutionId";
    private static readonly ActivitySource Source = new(SourceName);

    public static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole();
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            options.Rules.Clear();
            options.MinLevel = LogLevel.Trace;
            options.Rules.Add(new LoggerFilterRule(null, null, LogLevel.Trace,
                static (_, category, level) => category?.StartsWith("AgenteDIMP.Safe.", StringComparison.Ordinal) == true
                    && level >= LogLevel.Information));
        });
        logging.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(SourceName));
    }

    public static IApplicationBuilder UseRedactedTelemetry(this IApplicationBuilder application)
    {
        return application.Use(async (context, next) =>
        {
            var correlation = Guid.NewGuid();
            context.Items[CorrelationKey] = correlation;
            context.Response.Headers["X-Correlation-Id"] = correlation.ToString();
            if (Activity.Current is { } parent)
                foreach (var item in parent.Baggage.ToArray()) parent.SetBaggage(item.Key, null);
            using var activity = Source.StartActivity("http.request", ActivityKind.Internal);
            activity?.SetTag("dimp.correlation_id", correlation.ToString());
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(SourceName);
            try
            {
                await next(context);
                if (context.Response.StatusCode >= 400 && !context.Response.HasStarted && context.Response.ContentType is null)
                    await WriteProblem(context, context.Response.StatusCode,
                        context.Response.StatusCode == 404 ? "RESOURCE_NOT_FOUND" : "REQUEST_REJECTED", correlation);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                logger.LogWarning("HttpFailed {CorrelationId} {ExecutionId}", correlation, ExecutionId(context));
                if (context.Response.HasStarted) { context.Abort(); return; }
                var (status, code) = exception switch
                {
                    JsonException or BadHttpRequestException or ArgumentException or System.ComponentModel.DataAnnotations.ValidationException => (400, "INVALID_REQUEST"),
                    DbUpdateConcurrencyException => (409, "CONCURRENT_UPDATE"),
                    DbUpdateException or NpgsqlException => (503, "DEPENDENCY_UNAVAILABLE"),
                    SensitiveContentException => (500, "PRIVACY_GUARD_REJECTED"),
                    _ => (500, "INTERNAL_FAILURE")
                };
                context.Response.Clear();
                context.Response.Headers["X-Correlation-Id"] = correlation.ToString();
                await WriteProblem(context, status, code, correlation);
            }
            finally
            {
                activity?.SetTag("http.response.status_code", context.Response.StatusCode);
                if (ExecutionId(context) is { } executionId) activity?.SetTag("dimp.execution_id", executionId.ToString());
                logger.LogInformation("HttpCompleted {CorrelationId} {ExecutionId} {StatusCode}",
                    correlation, ExecutionId(context), context.Response.StatusCode);
            }
        });
    }

    private static Guid? ExecutionId(HttpContext context) =>
        context.Items.TryGetValue(ExecutionKey, out var value) && value is Guid id && id != Guid.Empty ? id : null;

    private static Task WriteProblem(HttpContext context, int status, string code, Guid correlation)
    {
        context.Response.StatusCode = status;
        var problem = new Problem
        {
            Code = code, Message = status == 404 ? "Recurso nao encontrado." : "Nao foi possivel concluir a solicitacao.",
            CorrelationId = correlation, ExecutionId = ExecutionId(context)
        };
        return context.Response.WriteAsJsonAsync(problem, ContractJson.Options, "application/problem+json", CancellationToken.None);
    }
}
