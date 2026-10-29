using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Api.Agente.Infrastructure.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Foundation;

public class RedactedTelemetryTests
{
    [Fact]
    public async Task ExceptionQueryAndIncomingCorrelationNeverLeakToProblemLogsOrDedicatedTrace()
    {
        const string sentinel = "sentinela-privada";
        var collector = new LogCollector();
        var captured = new ConcurrentBag<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RedactedTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => captured.Add(string.Join("|", activity.TagObjects.Select(x => x.Key + "=" + x.Value)
                .Concat(activity.Baggage.Select(x => x.Key + "=" + x.Value))))
        };
        ActivitySource.AddActivityListener(listener);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        RedactedTelemetry.ConfigureLogging(builder.Logging);
        builder.Logging.AddProvider(collector);
        await using var application = builder.Build();
        application.UseRedactedTelemetry();
        application.Run(_ => throw new InvalidOperationException("CPF 00000000000 " + sentinel));
        await application.StartAsync();
        using var client = application.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", sentinel);
        client.DefaultRequestHeaders.Add("baggage", "private=" + sentinel);
        var response = await client.GetAsync("/synthetic?credential=" + sentinel);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonNode.Parse(body)!;
        Assert.True(Guid.TryParse(problem["correlationId"]?.GetValue<string>(), out _));
        Assert.True(problem.AsObject().ContainsKey("executionId"));
        Assert.Null(problem["executionId"]);
        Assert.DoesNotContain(sentinel, body);
        Assert.DoesNotContain("00000000000", body);
        Assert.NotEmpty(collector.Messages);
        Assert.DoesNotContain(collector.Messages, value => value.Contains(sentinel) || value.Contains("00000000000"));
        Assert.NotEmpty(captured);
        Assert.DoesNotContain(captured, value => value.Contains(sentinel) || value.Contains("00000000000"));
    }

    private sealed class LogCollector : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CollectorLogger(Messages);
        public void Dispose() { }
        private sealed class CollectorLogger(ConcurrentBag<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception) + (exception?.Message ?? ""));
        }
    }
}
