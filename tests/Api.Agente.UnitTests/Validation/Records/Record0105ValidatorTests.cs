using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0105ValidatorTests
{
    private readonly Record0105Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0105RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0105", "00000000000191", "SUB ADQUIRENTE", "10", "1500,50"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCnpjFails()
    {
        var record = new DimpRecord(1, 0,
            ["0105", "11111111111111", "SUB ADQUIRENTE", "10", "1500,50"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cnpjEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0105.F02.TABLE");
        Assert.Equal(RuleResult.Failed, cnpjEval.Result);
    }
}
