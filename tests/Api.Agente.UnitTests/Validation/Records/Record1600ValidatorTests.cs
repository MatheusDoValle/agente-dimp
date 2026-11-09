using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1600ValidatorTests
{
    private readonly Record1600Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1600RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["1600", "", "CLI01", "202606", "50,00", "01", "POS01", "1", "1", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidPeriodoFails()
    {
        var record = new DimpRecord(1, 0,
            ["1600", "", "CLI01", "202613", "50,00", "01", "POS01", "1", "1", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var perEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1600.F04.TABLE");
        Assert.Equal(RuleResult.Failed, perEval.Result);
    }
}
