namespace Api.Agente.Domain.Validation;

public sealed class LayoutResolver : IDimpLayoutResolver
{
    public DimpLayoutResolution Resolve(DimpRecord firstRecord)
    {
        ArgumentNullException.ThrowIfNull(firstRecord);

        if (firstRecord.Fields.Length < 2 || firstRecord.Fields[0] != "0000")
        {
            return new DimpLayoutResolution(null, false, null);
        }

        var version = firstRecord.Fields[1];
        if (version == "10")
        {
            return new DimpLayoutResolution("10", true, RuleCatalog.SourceSha256);
        }

        // Unsupported version does not activate V10 fallback
        return new DimpLayoutResolution(version, false, null);
    }
}
