using System.Collections.Immutable;

namespace Api.Agente.Domain.Validation;

// Test-first contract for T031/T032. Raw fields live only during active processing.
// No parser is registered in the host and no endpoint is enabled by this contract.
public interface IDimpParser
{
    IAsyncEnumerable<DimpRecord> ReadAsync(Stream input, CancellationToken cancellationToken = default);
}
public enum DimpLineEnding { CrLf, Lf, Cr, None }
public sealed record DimpRecord(long LineNumber, long ByteOffset, ImmutableArray<string> Fields,
    bool HasInitialPipe, bool HasFinalPipe, DimpLineEnding LineEnding, ImmutableArray<string> DiagnosticCodes);
public interface IDimpLayoutResolver
{
    DimpLayoutResolution Resolve(DimpRecord firstRecord);
}
public sealed record DimpLayoutResolution(string? LayoutVersion, bool Supported, string? RevisionSha256);
