using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0209ValidatorTests
{
    private readonly Record0209Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0209RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0209", "00000000000191", "PROVEDOR POS", "RUA 1", "01234567", "3550308", "SP", "RESP", "11999999999", "contato@pos.com"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCnpjFails()
    {
        var record = new DimpRecord(1, 0,
            ["0209", "11111111111111", "PROVEDOR POS", "RUA 1", "01234567", "3550308", "SP", "RESP", "11999999999", "contato@pos.com"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cnpjEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0209.F02.TABLE");
        Assert.Equal(RuleResult.Failed, cnpjEval.Result);
    }
}
