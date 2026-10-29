using Api.Agente.Domain.Privacy;

namespace Api.Agente.Domain.Reports;

public enum EvidenceSource { Manual, MaskedFile, Execution }

public sealed class EvidenceSnapshot : IMaskedPayload
{
    private EvidenceSnapshot() { }
    public EvidenceSnapshot(Guid executionId, EvidenceSource source, MaskedText text, string? revisionSha256 = null,
        string? chunkId = null, IEnumerable<int>? pages = null, MaskedText? locator = null)
    {
        if (executionId == Guid.Empty || text.ExecutionId != executionId || locator is not null && locator.ExecutionId != executionId)
            throw new ArgumentException("Evidencia de outra execucao.");
        if (!Enum.IsDefined(source)) throw new ArgumentException("Origem invalida.");
        if (source == EvidenceSource.Manual && (revisionSha256 is null
            || !System.Text.RegularExpressions.Regex.IsMatch(revisionSha256, "^[a-fA-F0-9]{64}$")))
            throw new ArgumentException("Manual exige revisao identificada.");
        var pageList = pages?.Distinct().ToArray() ?? [];
        if (pageList.Any(x => x < 1)) throw new ArgumentException("Pagina invalida.");
        Id = Guid.NewGuid(); ExecutionId = executionId; Source = source; Text = text.Value;
        RevisionSha256 = revisionSha256; ChunkId = chunkId; Pages = pageList; Locator = locator?.Value;
    }
    public Guid Id { get; private set; }
    public Guid ExecutionId { get; private set; }
    public EvidenceSource Source { get; private set; }
    public string? RevisionSha256 { get; private set; }
    public string? ChunkId { get; private set; }
    public int[] Pages { get; private set; } = [];
    public string? Locator { get; private set; }
    public string Text { get; private set; } = "";
    public IReadOnlyDictionary<string, string> GetMaskedFields()
    {
        var fields = new Dictionary<string, string> { [nameof(Text)] = Text };
        if (Locator is not null) fields[nameof(Locator)] = Locator;
        return fields;
    }
}
