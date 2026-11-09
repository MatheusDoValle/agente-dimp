using System.Text.Json;
using Api.Agente.Domain.Validation;

namespace Api.Agente.UnitTests.Validation;

internal static class CatalogTestFiles
{
    public static string Root
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null && !File.Exists(Path.Combine(current.FullName,"AgenteDIMP.slnx"))) current=current.Parent;
            return current?.FullName ?? throw new InvalidOperationException("Repositorio de testes nao encontrado.");
        }
    }
    public static string PathFor(string path) => Path.Combine(Root,path);
    public static byte[] Bytes => File.ReadAllBytes(PathFor("docs/rules/dimp-v10/catalog.json"));
    public static RuleCatalog Load()
    {
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(PathFor("docs/rules/dimp-v10/manifest.json")));
        return RuleCatalog.Load(Bytes,manifest.RootElement.GetProperty("catalogSha256").GetString()!);
    }
}
