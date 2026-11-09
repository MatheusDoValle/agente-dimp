using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0001ValidatorTests
{
    private readonly Record0001Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void Valid0001RecordPasses(string indDad)
    {
        var record = new DimpRecord(1, 0, ["0001", indDad], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("9")]
    [InlineData("")]
    public void InvalidIndDadFails(string indDad)
    {
        var record = new DimpRecord(1, 0, ["0001", indDad], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var indEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0001.F02.TABLE");
        Assert.Equal(RuleResult.Failed, indEval.Result);
    }
}
