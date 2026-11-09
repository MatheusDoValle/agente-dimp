using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1001ValidatorTests
{
    private readonly Record1001Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void Valid1001RecordPasses(string indMov)
    {
        var record = new DimpRecord(1, 0, ["1001", indMov], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidIndMovFails()
    {
        var record = new DimpRecord(1, 0, ["1001", "2"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var eval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1001.F02.TABLE");
        Assert.Equal(RuleResult.Failed, eval.Result);
    }
}
