using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record9999ValidatorTests
{
    private readonly Record9999Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid9999RecordPasses()
    {
        var record = new DimpRecord(1, 0, ["9999", "19"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidQtdFails()
    {
        var record = new DimpRecord(1, 0, ["9999", "-1"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var qtdEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.9999.F02.TABLE");
        Assert.Equal(RuleResult.Failed, qtdEval.Result);
    }
}
