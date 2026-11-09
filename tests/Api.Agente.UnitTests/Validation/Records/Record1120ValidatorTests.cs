using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1120ValidatorTests
{
    private readonly Record1120Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1120RecordPassesStandardRulesAndBlocksUnderSF002()
    {
        var record = new DimpRecord(1, 0,
            ["1120", "SP", "", "00000000000191", "", "DEST01", "", "", "SP", "00000000000191", "PED123", "", "SELLER01", "3550308"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        var structEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1120.STRUCTURE");
        Assert.Equal(RuleResult.Passed, structEval.Result);

        var sf002Eval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1120.F14.TABLE");
        Assert.Equal(RuleResult.BlockedBySourceConflict, sf002Eval.Result);
        Assert.Equal(RuleApplicability.Undetermined, sf002Eval.Applicability);
    }

    [Fact]
    public void InvalidUfFails()
    {
        var record = new DimpRecord(1, 0,
            ["1120", "XX", "", "00000000000191", "", "DEST01", "", "", "SP", "00000000000191", "PED123", "", "SELLER01", "3550308"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var ufEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1120.F02.TABLE");
        Assert.Equal(RuleResult.Failed, ufEval.Result);
    }
}
