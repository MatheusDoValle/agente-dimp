namespace Api.Agente.Domain.Validation;

public interface IRecordValidator
{
    string RecordCode { get; }
    IReadOnlyList<RuleEvaluation> Validate(DimpRecord record, Guid executionId);
}
