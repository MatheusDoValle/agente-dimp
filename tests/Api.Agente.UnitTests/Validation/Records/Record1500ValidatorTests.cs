using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1500ValidatorTests
{
    private readonly Record1500Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1500RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["1500", "", "CLI01", "0", "0", "202606", "1000,00", "10", "POS01", "01", "1", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidPeriodoFails()
    {
        var record = new DimpRecord(1, 0,
            ["1500", "", "CLI01", "0", "0", "202613", "1000,00", "10", "POS01", "01", "1", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var perEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1500.F06.TABLE");
        Assert.Equal(RuleResult.Failed, perEval.Result);
    }
}
