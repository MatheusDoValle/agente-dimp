using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1110ValidatorTests
{
    private readonly Record1110Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1110RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["1110", "POS01", "20260615", "500,00", "5", "00000000000191"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidDateFails()
    {
        var record = new DimpRecord(1, 0,
            ["1110", "POS01", "20261301", "500,00", "5", "00000000000191"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var dtEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1110.F03.TABLE");
        Assert.Equal(RuleResult.Failed, dtEval.Result);
    }
}
