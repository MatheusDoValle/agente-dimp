using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0000ValidatorTests
{
    private readonly Record0000Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0000RecordPassesAllRules()
    {
        var record = new DimpRecord(
            1, 0,
            ["0000", "10", "1", "SP", "00000000000191", "EMPRESA TESTE", "20260601", "20260630", "0", "202606"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void Finalidade9ReturnsBlockedBySourceConflictUnderSF001()
    {
        var record = new DimpRecord(
            1, 0,
            ["0000", "10", "9", "SP", "00000000000191", "EMPRESA TESTE", "20260601", "20260630", "0", "202606"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var finEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0000.COD_FIN.DOMAIN");
        Assert.Equal(RuleResult.BlockedBySourceConflict, finEval.Result);
        Assert.Equal(RuleApplicability.Undetermined, finEval.Applicability);
    }

    [Fact]
    public void InvalidCnpjFailsF05Rule()
    {
        var record = new DimpRecord(
            1, 0,
            ["0000", "10", "1", "SP", "00000000000192", "EMPRESA TESTE", "20260601", "20260630", "0", "202606"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cnpjEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0000.F05.TABLE");
        Assert.Equal(RuleResult.Failed, cnpjEval.Result);
    }

    [Fact]
    public void InvertedDatesFailsF08Rule()
    {
        var record = new DimpRecord(
            1, 0,
            ["0000", "10", "1", "SP", "00000000000191", "EMPRESA TESTE", "20260630", "20260601", "0", "202606"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var dtFinEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0000.F08.TABLE");
        Assert.Equal(RuleResult.Failed, dtFinEval.Result);
    }

    [Fact]
    public void TruncatedFieldsFailsStructureRule()
    {
        var record = new DimpRecord(
            1, 0,
            ["0000", "10"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var structEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0000.STRUCTURE");
        Assert.Equal(RuleResult.Failed, structEval.Result);
    }
}
