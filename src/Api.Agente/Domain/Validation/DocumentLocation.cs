namespace Api.Agente.Domain.Validation;

public sealed class DocumentLocation
{
    private DocumentLocation() { }
    public DocumentLocation(long? line, long? byteOffset, string? recordCode, long? fieldPosition, string? unavailableReason)
    {
        if (line is < 1 || byteOffset is < 0 || fieldPosition is < 1)
            throw new ArgumentOutOfRangeException(nameof(line), "Localizacao invalida.");
        if (line is null && string.IsNullOrWhiteSpace(unavailableReason))
            throw new ArgumentException("Localizacao ausente exige motivo.");
        if (recordCode is not null && !System.Text.RegularExpressions.Regex.IsMatch(recordCode, "^[0-9]{4}$"))
            throw new ArgumentException("Codigo de registro invalido.");
        if (unavailableReason is not null && !System.Text.RegularExpressions.Regex.IsMatch(unavailableReason, "^[A-Z][A-Z0-9_]{0,63}$"))
            throw new ArgumentException("Usar codigo tecnico para localizacao indisponivel.");
        Line = line; ByteOffset = byteOffset; RecordCode = recordCode; FieldPosition = fieldPosition; UnavailableReason = unavailableReason;
    }
    public long? Line { get; private set; }
    public long? ByteOffset { get; private set; }
    public string? RecordCode { get; private set; }
    public long? FieldPosition { get; private set; }
    public string? UnavailableReason { get; private set; }
}
