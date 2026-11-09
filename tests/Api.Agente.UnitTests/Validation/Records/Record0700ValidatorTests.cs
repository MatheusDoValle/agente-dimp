using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0700ValidatorTests
{
    private readonly Record0700Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0700RecordPasses()
    {
        var record = new DimpRecord(1, 0, ["0700", "PROC-2026-001"], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void EmptyProcFails()
    {
        var record = new DimpRecord(1, 0, ["0700", ""], true, true, DimpLineEnding.CrLf, []);
        var evaluations = _validator.Validate(record, _executionId);

        var procEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0700.F02.TABLE");
        Assert.Equal(RuleResult.Failed, procEval.Result);
    }
}
