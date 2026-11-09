using Api.Agente.Domain.Validation;
using Api.Agente.Domain.Validation.Records;
using Xunit;

namespace Api.Agente.UnitTests.Validation.Records;

public class Record0005ValidatorTests
{
    private readonly Record0005Validator _validator = new();
    private readonly Guid _executionId = Guid.NewGuid();

    [Fact]
    public void Valid0005RecordPasses()
    {
        var record = new DimpRecord(1, 0,
            ["0005", "FANTASIA TESTE", "RUA TESTE", "01234567", "3550308", "SP", "NOME RESP", "1199999999", "contato@teste.com"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        Assert.NotEmpty(evaluations);
        Assert.All(evaluations, e => Assert.Equal(RuleResult.Passed, e.Result));
    }

    [Fact]
    public void InvalidCepFails()
    {
        var record = new DimpRecord(1, 0,
            ["0005", "FANTASIA TESTE", "RUA TESTE", "01234", "3550308", "SP", "NOME RESP", "1199999999", "contato@teste.com"],
            true, true, DimpLineEnding.CrLf, []);

        var evaluations = _validator.Validate(record, _executionId);

        var cepEval = Assert.Single(evaluations, e => e.RuleId == "DIMP10.0005.F04.TABLE");
        Assert.Equal(RuleResult.Failed, cepEval.Result);
    }
}
