using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1115ValidatorTests
{
    private readonly Record1115Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1115RecordPassesStandardRulesAndBlocksUnderSF003()
    {
        var record = new DimpRecord(1, 0,
            ["1115", "123456", "AUT01", "TRANS01", "0", "01", "123000", "150,00", "01", "", "1", "1"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        var structEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1115.STRUCTURE");
        Assert.Equal(RuleResult.Passed, structEval.Result);

        var sf003Eval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1115.F12.TABLE");
        Assert.Equal(RuleResult.BlockedBySourceConflict, sf003Eval.Result);
        Assert.Equal(RuleApplicability.Undetermined, sf003Eval.Applicability);
    }

    [Fact]
    public void InvalidHoraFails()
    {
        var record = new DimpRecord(1, 0,
            ["1115", "123456", "AUT01", "TRANS01", "0", "01", "250000", "150,00", "01", "", "1", "1"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var horaEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1115.F07.TABLE");
        Assert.Equal(RuleResult.Failed, horaEval.Result);
    }
}
