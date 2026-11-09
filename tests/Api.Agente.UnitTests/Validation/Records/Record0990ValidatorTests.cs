using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0990ValidatorTests
{
    private readonly Record0990Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0990RecordPasses()
    {
        var record = new DimpRecord(1, 0, ["0990", "4"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void InvalidQtdFails(string qtd)
    {
        var record = new DimpRecord(1, 0, ["0990", qtd], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var qtdEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0990.F02.TABLE");
        Assert.Equal(RuleResult.Failed, qtdEval.Result);
    }
}
