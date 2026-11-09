using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1990ValidatorTests
{
    private readonly Record1990Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1990RecordPasses()
    {
        var record = new DimpRecord(1, 0, ["1990", "2"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidQtdFails()
    {
        var record = new DimpRecord(1, 0, ["1990", "0"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var qtdEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1990.F02.TABLE");
        Assert.Equal(RuleResult.Failed, qtdEval.Result);
    }
}
