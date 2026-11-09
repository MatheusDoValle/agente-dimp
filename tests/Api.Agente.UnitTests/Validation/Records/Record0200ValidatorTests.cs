using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0200ValidatorTests
{
    private readonly Record0200Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0200RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0200", "POS01", "LOG123", "1", "1", "GERTEC", "1", "00000000000191"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCnpjTermFails()
    {
        var record = new DimpRecord(1, 0,
            ["0200", "POS01", "LOG123", "1", "1", "GERTEC", "1", "11111111111111"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cnpjEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0200.F08.TABLE");
        Assert.Equal(RuleResult.Failed, cnpjEval.Result);
    }
}
