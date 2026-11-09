using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0201ValidatorTests
{
    private readonly Record0201Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0201RecordPasses()
    {
        var record = new DimpRecord(1, 0, ["0201", "CLI123"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void EmptyClienteFails()
    {
        var record = new DimpRecord(1, 0, ["0201", ""], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var cliEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0201.F02.TABLE");
        Assert.Equal(RuleResult.Failed, cliEval.Result);
    }
}
