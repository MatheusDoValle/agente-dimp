using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Api.Agente.Infrastructure.Dimp;

namespace Api.Agente.Domain.Validation;

public sealed class DimpParser : IDimpParser
{
    private static readonly FrozenDictionary<string, string> RecordParents = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["0001"] = "0000",
        ["0002"] = "0001",
        ["0005"] = "0001",
        ["0006"] = "0001",
        ["0100"] = "0001",
        ["0105"] = "0100",
        ["0109"] = "0100",
        ["0200"] = "0001",
        ["0201"] = "0200",
        ["0209"] = "0200",
        ["0300"] = "0001",
        ["0600"] = "0001",
        ["0700"] = "0001",
        ["0990"] = "0000",
        ["1001"] = "0000",
        ["1100"] = "1001",
        ["1110"] = "1100",
        ["1115"] = "1110",
        ["1120"] = "1100",
        ["1200"] = "1001",
        ["1220"] = "1200",
        ["1500"] = "1001",
        ["1600"] = "1001",
        ["1990"] = "1001",
        ["9001"] = "0000",
        ["9900"] = "9001",
        ["9990"] = "9001",
        ["9999"] = "0000",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly DimpStreamReader _reader = new();

    public async IAsyncEnumerable<DimpRecord> ReadAsync(
        Stream input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var activeStack = new List<string>(8);

        await foreach (var line in _reader.ReadLinesAsync(input, cancellationToken).ConfigureAwait(false))
        {
            var diagnosticCodes = ImmutableArray<string>.Empty;

            if (!line.Fields.IsEmpty)
            {
                var recordCode = line.Fields[0];

                if (recordCode == "0000")
                {
                    activeStack.Clear();
                    activeStack.Add("0000");
                }
                else if (RecordParents.TryGetValue(recordCode, out var expectedParent))
                {
                    var parentIndex = activeStack.LastIndexOf(expectedParent);
                    if (parentIndex >= 0)
                    {
                        // Unwind everything above the expected parent
                        activeStack.RemoveRange(parentIndex + 1, activeStack.Count - (parentIndex + 1));
                        activeStack.Add(recordCode);
                    }
                    else
                    {
                        diagnosticCodes = ["ORPHAN_RECORD"];
                    }
                }
            }

            yield return new DimpRecord(
                line.LineNumber,
                line.ByteOffset,
                line.Fields,
                line.HasInitialPipe,
                line.HasFinalPipe,
                line.LineEnding,
                diagnosticCodes);
        }
    }
}
