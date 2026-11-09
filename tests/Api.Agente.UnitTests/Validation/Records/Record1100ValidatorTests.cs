using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1100ValidatorTests
{
    private readonly Record1100Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1100RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["1100", "", "CLI01", "0", "0", "20260601", "20260630", "15000,00", "100"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvertedDatesFails()
    {
        var record = new DimpRecord(1, 0,
            ["1100", "", "CLI01", "0", "0", "20260630", "20260601", "15000,00", "100"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var dtFinEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1100.F07.TABLE");
        Assert.Equal(RuleResult.Failed, dtFinEval.Result);
    }
}
