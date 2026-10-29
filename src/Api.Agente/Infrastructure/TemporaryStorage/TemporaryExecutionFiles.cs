using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Api.Agente.Infrastructure.Configuration;

namespace Api.Agente.Infrastructure.TemporaryStorage;

internal sealed class TemporaryExecutionFiles
{
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    internal string Root { get; }
    internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    internal TemporaryExecutionFiles(TemporaryStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath) || !Path.IsPathFullyQualified(options.RootPath))
            throw new ArgumentException("Raiz temporaria absoluta obrigatoria.");
        Root = Path.GetFullPath(options.RootPath).TrimEnd(Path.DirectorySeparatorChar);
        if (Root == Path.GetPathRoot(Root)?.TrimEnd(Path.DirectorySeparatorChar) || Root.StartsWith(@"\\"))
            throw new ArgumentException("Raiz temporaria deve ser um diretorio local dedicado.");
    }

    internal SemaphoreSlim Gate(Guid executionId)
    {
        RequireId(executionId);
        var hash = HashCode.Combine(Root, executionId) & int.MaxValue;
        return Gates[hash % Gates.Length];
    }

    internal string DirectoryFor(Guid executionId)
    {
        RequireId(executionId);
        return RequireChild(Path.Combine(Root, executionId.ToString("N")));
    }
    internal string MapPath(Guid executionId) => RequireChild(Path.Combine(DirectoryFor(executionId), "mask-map.json"));
    internal string ManifestPath(Guid executionId) => RequireChild(Path.Combine(DirectoryFor(executionId), "manifest.json"));
    internal string ClosedPath(Guid executionId) => RequireChild(Path.Combine(Root, "_closed", executionId.ToString("N") + ".json"));

    internal void EnsureWritable(Guid executionId)
    {
        if (File.Exists(ClosedPath(executionId))) throw new InvalidOperationException("Execucao em limpeza ou encerrada.");
        SecureDirectory(Root);
        SecureDirectory(DirectoryFor(executionId));
    }

    internal void SecureDirectory(string path)
    {
        RequireInsideRoot(path);
        EnsureNoLinks(path);
        Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User ?? throw new UnauthorizedAccessException("Identidade do processo indisponivel.");
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.NotContentIndexed);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    internal FileStream CreatePrivateFile(string path)
    {
        RequireChild(path);
        EnsureNoLinks(Path.GetDirectoryName(path)!);
        var options = new FileStreamOptions
        {
            Access = FileAccess.Write, Mode = FileMode.CreateNew, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough, BufferSize = 65536
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }

    internal async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        RequireChild(path);
        var temporary = RequireChild(path + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = CreatePrivateFile(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            EnsureNoLinks(path);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal string RequireChild(string path)
    {
        var absolute = Path.GetFullPath(path);
        if (!absolute.StartsWith(Root + Path.DirectorySeparatorChar, PathComparison))
            throw new InvalidOperationException("Caminho fora da area temporaria.");
        return absolute;
    }

    internal void EnsureNoLinks(string path)
    {
        var item = new DirectoryInfo(Path.GetFullPath(path));
        while (item is not null)
        {
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Links nao permitidos na area temporaria.");
            item = item.Parent;
        }
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Link de arquivo nao permitido.");
    }

    private void RequireInsideRoot(string path)
    {
        if (!string.Equals(Path.GetFullPath(path), Root, PathComparison)) RequireChild(path);
    }
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static void RequireId(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Execucao obrigatoria."); }
}
