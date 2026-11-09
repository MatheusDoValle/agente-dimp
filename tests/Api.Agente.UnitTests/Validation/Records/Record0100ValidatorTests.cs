using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0100ValidatorTests
{
    private readonly Record0100Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0100WithCnpjPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0100", "CLI01", "00000000000191", "", "FANTASIA", "RUA 1", "01234567", "3550308", "SP", "RESP", "1199999999", "email@teste.com", "20260101", "0"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void Valid0100WithCpfPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0100", "CLI01", "", "12345678909", "FANTASIA", "RUA 1", "01234567", "3550308", "SP", "RESP", "1199999999", "email@teste.com", "20260101", "0"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidDocFails()
    {
        var record = new DimpRecord(1, 0,
            ["0100", "CLI01", "111", "222", "FANTASIA", "RUA 1", "01234567", "3550308", "SP", "RESP", "1199999999", "email@teste.com", "20260101", "0"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var docEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0100.F03.TABLE");
        Assert.Equal(RuleResult.Failed, docEval.Result);
    }
}
