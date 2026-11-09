using System.Diagnostics;
using System.Text;
using Api.Agente.Application.Contracts;
using Api.Agente.Application.Reports;
using Api.Agente.Domain;
using Api.Agente.Domain.Reports;
using Api.Agente.Domain.Validation;
using Xunit;

namespace AgenteDIMP.IntegrationTests.Performance;

public class SmallFileBaselineTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "dimp-v10", "valido.txt");

    [Fact]
    public async Task Measure_SmallFileBaseline_RecordsStageDurations_AndMemoryFootprint()
    {
        var content = File.Exists(FixturePath)
            ? await File.ReadAllTextAsync(FixturePath)
            : "0000|10|1|12345678000195|EMPRESA TESTE ME|2026-01-01|2026-01-31|0.1.0|\n" +
              "0005|CONTATO|11999998888|contato@empresa.com.br|\n" +
              "9999|2|\n";

        var bytes = Encoding.UTF8.GetBytes(content);
        var fileSize = bytes.Length;

        var memoryBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        // 1. Parsing stage
        using var stream = new MemoryStream(bytes);
        var parser = new DimpParser();
        var parsedRecords = new List<DimpRecord>();
        await foreach (var record in parser.ReadAsync(stream, CancellationToken.None))
        {
            parsedRecords.Add(record);
        }
        var parseDurationMs = sw.ElapsedMilliseconds;

        // 2. Validation stage
        async IAsyncEnumerable<DimpRecord> ToAsyncEnumerable(IEnumerable<DimpRecord> items)
        {
            foreach (var item in items)
            {
                yield return item;
            }
        }

        var validationSw = Stopwatch.StartNew();
        var validationEngine = new DimpValidationEngine();
        var executionId = Guid.NewGuid();
        var evaluations = new List<RuleEvaluation>();
        await foreach (var eval in validationEngine.ValidateAsync(ToAsyncEnumerable(parsedRecords), null, executionId, CancellationToken.None))
        {
            evaluations.Add(eval);
        }
        var validationDurationMs = validationSw.ElapsedMilliseconds;

        // 3. Composition stage
        var composeSw = Stopwatch.StartNew();
        var exec = new Execution(executionId, Guid.NewGuid(), "perf-test", "ctx", DateTimeOffset.UtcNow);
        exec.Start(DateTimeOffset.UtcNow);
        exec.SetSource("10", "FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A", "0.1.0", Guid.NewGuid());
        exec.BeginCleanup(DateTimeOffset.UtcNow);
        exec.CompleteAfterCleanup(ExecutionState.Completed, DateTimeOffset.UtcNow, hasReport: true);

        var report = ReportComposer.Compose(
            exec,
            evaluations,
            [],
            [],
            null,
            null);
        var composeDurationMs = composeSw.ElapsedMilliseconds;
        sw.Stop();

        var memoryAllocated = GC.GetAllocatedBytesForCurrentThread() - memoryBefore;

        // Assert baseline limits
        Assert.True(fileSize > 0, "Arquivo deve ter tamanho > 0 bytes");
        Assert.True(parsedRecords.Count >= 2, "Arquivo deve conter no mínimo 2 registros");
        Assert.True(sw.ElapsedMilliseconds < 5000, "Processamento de arquivo pequeno deve ocorrer em menos de 5 segundos");
        Assert.NotNull(report);
        Assert.Equal(executionId, report.ExecutionId);

        // Verification metrics
        Assert.True(parseDurationMs >= 0);
        Assert.True(validationDurationMs >= 0);
        Assert.True(composeDurationMs >= 0);
        Assert.True(memoryAllocated > 0);
    }
}
