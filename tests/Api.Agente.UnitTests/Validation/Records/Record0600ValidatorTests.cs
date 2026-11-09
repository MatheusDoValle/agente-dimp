using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0600ValidatorTests
{
    private readonly Record0600Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0600RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0600", "1", "00000000000191", "1", "AUTORIZADORA", "RUA 1", "01234567", "3550308", "SP", "RESP", "1199999999", "contato@aut.com", "20260101", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCnpjFails()
    {
        var record = new DimpRecord(1, 0,
            ["0600", "1", "11111111111111", "1", "AUTORIZADORA", "RUA 1", "01234567", "3550308", "SP", "RESP", "1199999999", "contato@aut.com", "20260101", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cnpjEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0600.F03.TABLE");
        Assert.Equal(RuleResult.Failed, cnpjEval.Result);
    }
}
