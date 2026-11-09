using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1200ValidatorTests
{
    private readonly Record1200Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1200RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["1200", "", "CLI01", "POS01", "123456", "AUT01", "TRANS01", "20260601", "20260605", "1", "100,00", "100,00", "01", "1", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvertedDatesFails()
    {
        var record = new DimpRecord(1, 0,
            ["1200", "", "CLI01", "POS01", "123456", "AUT01", "TRANS01", "20260610", "20260605", "1", "100,00", "100,00", "01", "1", ""],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var dtCancEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1200.F09.TABLE");
        Assert.Equal(RuleResult.Failed, dtCancEval.Result);
    }
}
