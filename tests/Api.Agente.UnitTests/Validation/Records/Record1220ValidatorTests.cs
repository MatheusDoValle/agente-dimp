using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record1220ValidatorTests
{
    private readonly Record1220Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid1220RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["1220", "MG", "", "00000000000191", "", "DEST01", "PED999"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidUfFails()
    {
        var record = new DimpRecord(1, 0,
            ["1220", "ZZ", "", "00000000000191", "", "DEST01", "PED999"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var ufEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.1220.F02.TABLE");
        Assert.Equal(RuleResult.Failed, ufEval.Result);
    }
}
