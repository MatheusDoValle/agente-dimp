namespace Api.Agente.Domain.Privacy;

public sealed class MaskedText
{
    internal MaskedText(Guid executionId, string value) { ExecutionId = executionId; Value = value; }
    public Guid ExecutionId { get; }
    public string Value { get; }
}

public interface IMaskedPayload
{
    Guid ExecutionId { get; }
    IReadOnlyDictionary<string, string> GetMaskedFields();
}
