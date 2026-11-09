using Api.Agente.Domain.Validation;
using Xunit;

namespace Api.Agente.UnitTests.Validation;

public class DimpValidationEngineTests
{
    private readonly DimpValidationEngine _engine = new();
    private readonly DimpParser _parser = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public async Task ValidZeroFilePassesDeterministicValidation()
    {
        var path = CatalogTestFiles.PathFor("tests/fixtures/dimp-v10/zerado-sintetico.dimp");
        using var stream = File.OpenRead(path);

        var evaluations = new List<RuleEvaluation>();
        await foreach (var ev in _engine.ValidateAsync(_parser.ReadAsync(stream), null, _executionId))
        {
            evaluations.Add(ev);
        }

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public async Task OrphanRecordProducesFailure()
    {
        var path = CatalogTestFiles.PathFor("tests/fixtures/dimp-v10/filho-orfao.dimp");
        using var stream = File.OpenRead(path);

        var evaluations = new List<RuleEvaluation>();
        await foreach (var ev in _engine.ValidateAsync(_parser.ReadAsync(stream), null, _executionId))
        {
            evaluations.Add(ev);
        }

        Assert.Contains(evaluations, e => e.Result == RuleResult.Failed);
    }

    [Fact]
    public async Task Sf001ProducesBlockedBySourceConflict()
    {
        var path = CatalogTestFiles.PathFor("tests/fixtures/dimp-v10/sf001-finalidade9.dimp");
        using var stream = File.OpenRead(path);

        var evaluations = new List<RuleEvaluation>();
        await foreach (var ev in _engine.ValidateAsync(_parser.ReadAsync(stream), null, _executionId))
        {
            evaluations.Add(ev);
        }

        Assert.Contains(evaluations, e => e.Result == RuleResult.BlockedBySourceConflict);
    }
}
