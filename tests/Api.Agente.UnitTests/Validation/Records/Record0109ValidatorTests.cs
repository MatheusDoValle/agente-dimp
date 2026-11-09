using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0109ValidatorTests
{
    private readonly Record0109Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0109RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0109", "00000000000191", "5", "5000,00"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCnpjFails()
    {
        var record = new DimpRecord(1, 0,
            ["0109", "11111111111111", "5", "5000,00"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cnpjEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0109.F02.TABLE");
        Assert.Equal(RuleResult.Failed, cnpjEval.Result);
    }
}
