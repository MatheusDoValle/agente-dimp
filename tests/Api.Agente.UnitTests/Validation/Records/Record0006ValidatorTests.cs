using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0006ValidatorTests
{
    private readonly Record0006Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0006RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0006", "RESPONSAVEL TESTE", "12345678909", "resp@teste.com"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCpfFails()
    {
        var record = new DimpRecord(1, 0,
            ["0006", "RESPONSAVEL TESTE", "11111111111", "resp@teste.com"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cpfEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0006.F03.TABLE");
        Assert.Equal(RuleResult.Failed, cpfEval.Result);
    }
}
