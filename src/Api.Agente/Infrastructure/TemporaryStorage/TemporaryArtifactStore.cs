using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Api.Agente.Infrastructure.Configuration;

namespace Api.Agente.Infrastructure.TemporaryStorage;

public enum TemporaryArtifactKind { Original, ParserIndex, MaskMap, UploadPartial }
public sealed record TemporaryArtifact(Guid ExecutionId, Guid ArtifactId, TemporaryArtifactKind Kind,
    DateTimeOffset CreatedAt, long ByteSize, string Sha256, DateTimeOffset? DeletedAt = null, string CleanupState = "active");

public interface ITemporaryArtifactStore
{
    Task<TemporaryArtifact> CreateAsync(Guid executionId, TemporaryArtifactKind kind, Stream source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemporaryArtifact>> ListAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(Guid executionId, Guid artifactId, CancellationToken cancellationToken = default);
    Task<bool> DeleteExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);
}

public sealed class TemporaryArtifactStore(TemporaryStorageOptions options, TimeProvider clock) : ITemporaryArtifactStore
{
    private static readonly ConcurrentDictionary<string, int> Readers = new();
    private readonly TemporaryExecutionFiles _files = new(options);

    public async Task<TemporaryArtifact> CreateAsync(Guid executionId, TemporaryArtifactKind kind, Stream source, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(kind) || kind == TemporaryArtifactKind.MaskMap)
            throw new ArgumentException("Mapa de mascaramento e gerenciado pelo sanitizer.");
        ArgumentNullException.ThrowIfNull(source);
        var gate = _files.Gate(executionId);
        await gate.WaitAsync(cancellationToken);
        string? path = null;
        try
        {
            _files.EnsureWritable(executionId);
            var id = Guid.NewGuid();
            path = PathFor(executionId, id, kind);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long size = 0;
            await using (var target = _files.CreatePrivateFile(path))
            {
                var buffer = new byte[65536];
                int count;
                while ((count = await source.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    hash.AppendData(buffer, 0, count);
                    size = checked(size + count);
                }
                await target.FlushAsync(cancellationToken);
                target.Flush(flushToDisk: true);
            }
            var artifact = new TemporaryArtifact(executionId, id, kind, clock.GetUtcNow().ToUniversalTime(), size,
                Convert.ToHexString(hash.GetHashAndReset()));
            var manifest = ReadManifest(executionId);
            manifest.Add(artifact);
            await _files.WriteJsonAsync(_files.ManifestPath(executionId), manifest, cancellationToken);
            return artifact;
        }
        catch
        {
            if (path is not null && File.Exists(path)) File.Delete(path);
            throw;
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<TemporaryArtifact>> ListAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var gate = _files.Gate(executionId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var result = ReadManifest(executionId);
            var mapPath = _files.MapPath(executionId);
            if (File.Exists(mapPath))
            {
                _files.EnsureNoLinks(mapPath);
                await using var source = File.OpenRead(mapPath);
                var hash = await SHA256.HashDataAsync(source, cancellationToken);
                result.Add(new TemporaryArtifact(executionId, executionId, TemporaryArtifactKind.MaskMap,
                    new DateTimeOffset(File.GetCreationTimeUtc(mapPath), TimeSpan.Zero), source.Length, Convert.ToHexString(hash)));
            }
            return result;
        }
        finally { gate.Release(); }
    }

    public async Task<Stream> OpenReadAsync(Guid executionId, Guid artifactId, CancellationToken cancellationToken = default)
    {
        var gate = _files.Gate(executionId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_files.ClosedPath(executionId))) throw new InvalidOperationException("Execucao em limpeza.");
            var artifact = ReadManifest(executionId).SingleOrDefault(x => x.ArtifactId == artifactId)
                ?? throw new FileNotFoundException("Artefato nao encontrado.");
            if (artifact.Kind == TemporaryArtifactKind.MaskMap) throw new InvalidOperationException("Mapa nao e conteudo de leitura do parser.");
            var path = PathFor(executionId, artifactId, artifact.Kind);
            _files.EnsureNoLinks(path);
            var key = _files.DirectoryFor(executionId);
            Readers.AddOrUpdate(key, 1, (_, value) => value + 1);
            try
            {
                return new TrackedReadStream(path, () =>
                {
                    gate.Wait();
                    try
                    {
                        if (Readers.TryGetValue(key, out var count) && count > 1) Readers[key] = count - 1;
                        else Readers.TryRemove(key, out _);
                    }
                    finally { gate.Release(); }
                });
            }
            catch
            {
                if (Readers.TryGetValue(key, out var count) && count > 1) Readers[key] = count - 1;
                else Readers.TryRemove(key, out _);
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<bool> DeleteExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var gate = _files.Gate(executionId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _files.SecureDirectory(_files.Root);
            _files.SecureDirectory(Path.GetDirectoryName(_files.ClosedPath(executionId))!);
            await _files.WriteJsonAsync(_files.ClosedPath(executionId), new
            {
                executionId, cleanupRequestedAt = clock.GetUtcNow().ToUniversalTime()
            }, cancellationToken);
            var directory = _files.DirectoryFor(executionId);
            if (Readers.TryGetValue(directory, out var readers) && readers > 0) return false;
            if (Directory.Exists(directory)) DeleteVerifiedTree(directory, cancellationToken);
            return !Directory.Exists(directory);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        finally { gate.Release(); }
    }

    private List<TemporaryArtifact> ReadManifest(Guid executionId)
    {
        var path = _files.ManifestPath(executionId);
        _files.EnsureNoLinks(path);
        if (!File.Exists(path)) return [];
        var result = JsonSerializer.Deserialize<List<TemporaryArtifact>>(File.ReadAllText(path), TemporaryExecutionFiles.JsonOptions)
            ?? throw new IOException("Manifesto temporario invalido.");
        if (result.Any(x => x.ExecutionId != executionId || x.ArtifactId == Guid.Empty || !Enum.IsDefined(x.Kind)
            || x.Kind == TemporaryArtifactKind.MaskMap || x.ByteSize < 0))
            throw new IOException("Manifesto de outro escopo ou invalido.");
        return result;
    }

    private string PathFor(Guid executionId, Guid artifactId, TemporaryArtifactKind kind)
    {
        if (artifactId == Guid.Empty || !Enum.IsDefined(kind)) throw new ArgumentException("Artefato invalido.");
        var extension = kind switch
        {
            TemporaryArtifactKind.Original => ".original",
            TemporaryArtifactKind.ParserIndex => ".sqlite",
            TemporaryArtifactKind.UploadPartial => ".upload",
            _ => throw new InvalidOperationException("Mapa e privado do sanitizer.")
        };
        return _files.RequireChild(Path.Combine(_files.DirectoryFor(executionId), artifactId.ToString("N") + extension));
    }

    private void DeleteVerifiedTree(string directory, CancellationToken cancellationToken)
    {
        _files.RequireChild(directory);
        _files.EnsureNoLinks(directory);
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _files.RequireChild(file);
            _files.EnsureNoLinks(file);
            File.Delete(file);
        }
        foreach (var child in Directory.EnumerateDirectories(directory)) DeleteVerifiedTree(child, cancellationToken);
        Directory.Delete(directory, recursive: false);
    }

    private sealed class TrackedReadStream(string path, Action onClosed)
        : FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan)
    {
        private Action? _onClosed = onClosed;
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) Interlocked.Exchange(ref _onClosed, null)?.Invoke();
        }
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            Interlocked.Exchange(ref _onClosed, null)?.Invoke();
            GC.SuppressFinalize(this);
        }
    }
}
