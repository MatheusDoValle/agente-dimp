using Api.Agente.Domain;
using Api.Agente.Domain.Validation;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Agente.UnitTests.Foundation;

public class ExecutionAndCoverageTests
{
    [Fact]
    public void HostGeneratesIndependentIdsAndUtcTimestamps()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExecutionContext:OperatorId"] = "test-operator",
            ["ExecutionContext:ContextId"] = "test-local",
            ["ExecutionContext:IdentityAssurance"] = "unverified_local_configuration"
        }).Build();
        var factory = new Application.ExecutionContext(TimeProvider.System, configuration);
        var first = factory.CreateExecution();
        var second = factory.CreateExecution();
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.CorrelationId, second.CorrelationId);
        Assert.Equal(TimeSpan.Zero, first.CreatedAt.Offset);
        Assert.Null(first.EndedAt);
        first.Start(first.CreatedAt.AddSeconds(1));
        Assert.Equal(ExecutionState.Processing, first.Status);
        Assert.Null(first.EndedAt);
        Assert.Equal(ExecutionVerdict.Inconclusive, first.Verdict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("00000000000")]
    [InlineData("operator@example.invalid")]
    public void MissingOrPersonalAliasIsRejected(string alias)
    {
        Assert.Throws<ArgumentException>(() => new Execution(Guid.NewGuid(), Guid.NewGuid(), alias, "test", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SameRuleCanPassAndFailAtDifferentOccurrencesWithoutDoubleCounting()
    {
        var id = Guid.NewGuid();
        var rows = new[]
        {
            new RuleEvaluation(id, "DIMP10.TEST", "line:1", RuleApplicability.Applicable, RuleResult.Passed),
            new RuleEvaluation(id, "DIMP10.TEST", "line:2", RuleApplicability.Applicable, RuleResult.Failed),
            new RuleEvaluation(id, "DIMP10.OPTIONAL", "file", RuleApplicability.NotApplicable, RuleResult.NotApplicable),
            new RuleEvaluation(id, "DIMP10.PENDING", "file", RuleApplicability.Undetermined, RuleResult.NotExecuted),
            new RuleEvaluation(id, "DIMP10.CONFLICT", "file", RuleApplicability.Undetermined, RuleResult.BlockedBySourceConflict)
        };
        var totals = CoverageTotals.FromEvaluations(rows);
        Assert.Equal(new CoverageTotals(4, 1, 1, 1, 1), totals);
        Assert.Equal(totals.Applicable, totals.Passed + totals.Failed + totals.NotExecuted + totals.BlockedBySourceConflict);
        Assert.Throws<ArgumentException>(() => CoverageTotals.FromEvaluations([rows[0], rows[0]]));
    }

    [Fact]
    public void OccurrenceNeverStoresRawIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new RuleEvaluation(Guid.NewGuid(), "DIMP10.TEST", "cpf:00000000000",
            RuleApplicability.Applicable, RuleResult.Passed));
    }

    [Fact]
    public void UnknownApplicabilityCannotBecomePassedOrNotApplicable()
    {
        Assert.Throws<ArgumentException>(() => new RuleEvaluation(Guid.NewGuid(), "DIMP10.TEST", "file", RuleApplicability.Undetermined, RuleResult.Passed));
    }
}
