using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Api.Agente.Domain.Validation;

namespace Api.Agente.Infrastructure.Dimp;

public sealed class DimpStreamReader
{
    private const int BufferSize = 4096;

    public async IAsyncEnumerable<RawDimpLine> ReadLinesAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var buffer = new byte[BufferSize];
        var lineBytes = new List<byte>(256);
        long lineNumber = 0;
        long currentLineOffset = 0;
        long currentStreamOffset = 0;
        var pendingCr = false;

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < bytesRead; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var b = buffer[i];

                if (pendingCr)
                {
                    pendingCr = false;
                    if (b == 0x0A) // \r\n
                    {
                        lineNumber++;
                        yield return ParseRawLine(lineNumber, currentLineOffset, lineBytes, DimpLineEnding.CrLf);
                        lineBytes.Clear();
                        currentStreamOffset++;
                        currentLineOffset = currentStreamOffset;
                        continue;
                    }

                    // \r not followed by \n
                    lineNumber++;
                    yield return ParseRawLine(lineNumber, currentLineOffset, lineBytes, DimpLineEnding.Cr);
                    lineBytes.Clear();
                    currentLineOffset = currentStreamOffset;
                    // Current byte b belongs to next line!
                }

                if (b == 0x0D) // \r
                {
                    pendingCr = true;
                    currentStreamOffset++;
                    continue;
                }

                if (b == 0x0A) // \n
                {
                    currentStreamOffset++;
                    lineNumber++;
                    yield return ParseRawLine(lineNumber, currentLineOffset, lineBytes, DimpLineEnding.Lf);
                    lineBytes.Clear();
                    currentLineOffset = currentStreamOffset;
                    continue;
                }

                lineBytes.Add(b);
                currentStreamOffset++;
            }
        }

        if (pendingCr)
        {
            lineNumber++;
            yield return ParseRawLine(lineNumber, currentLineOffset, lineBytes, DimpLineEnding.Cr);
            lineBytes.Clear();
        }
        else if (lineBytes.Count > 0)
        {
            lineNumber++;
            yield return ParseRawLine(lineNumber, currentLineOffset, lineBytes, DimpLineEnding.None);
            lineBytes.Clear();
        }
    }

    private static RawDimpLine ParseRawLine(long lineNumber, long byteOffset, List<byte> lineBytes, DimpLineEnding lineEnding)
    {
        if (lineBytes.Count == 0)
        {
            return new RawDimpLine(lineNumber, byteOffset, ImmutableArray<string>.Empty, false, false, lineEnding);
        }

        var hasInitialPipe = lineBytes[0] == 0x7C;
        var hasFinalPipe = lineBytes[^1] == 0x7C;

        var lineText = Encoding.Latin1.GetString(lineBytes.ToArray());

        ImmutableArray<string> fields;
        if (lineBytes.Count == 1 && hasInitialPipe)
        {
            fields = ImmutableArray<string>.Empty;
        }
        else
        {
            var content = lineText;
            if (hasInitialPipe && content.Length > 0)
            {
                content = content[1..];
            }
            if (hasFinalPipe && content.Length > 0)
            {
                content = content[..^1];
            }

            var parts = content.Split('|');
            fields = [.. parts];
        }

        return new RawDimpLine(lineNumber, byteOffset, fields, hasInitialPipe, hasFinalPipe, lineEnding);
    }
}

public sealed record RawDimpLine(
    long LineNumber,
    long ByteOffset,
    ImmutableArray<string> Fields,
    bool HasInitialPipe,
    bool HasFinalPipe,
    DimpLineEnding LineEnding);
