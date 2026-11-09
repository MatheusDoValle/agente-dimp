using System.Security.Cryptography;
using System.Text.Json;
using Api.Agente.Domain.Validation;
using AgenteDIMP.IntegrationTests.Foundation;
using Xunit;

namespace AgenteDIMP.IntegrationTests.US1;

public class RuleCoverageTests
{
    private static string PathFor(string p)=>Path.Combine(FoundationSupport.RepositoryRoot(),p);
    [Fact]
    [Trait("Category","SpecificationPending")]
    public void EverySourceRuleRequiresIndependentOraclesBeforeFiscalCoverageCanBeAccepted()
    {
        using var m=JsonDocument.Parse(File.ReadAllBytes(PathFor("docs/rules/dimp-v10/manifest.json")));
        var catalog=RuleCatalog.Load(File.ReadAllBytes(PathFor("docs/rules/dimp-v10/catalog.json")),m.RootElement.GetProperty("catalogSha256").GetString()!);
        var pending=catalog.Rules.Where(r=>r.Interpretation!="resolved" ||
            new[]{"positive","negative","conditional"}.Any(kind=>!r.Tests.Any(t=>t.Kind==kind
                && t.Fixture is not null && t.ExpectedResult is not null && t.Status=="implemented"))).Select(r=>r.Id).ToArray();
        Assert.True(pending.Length==0,$"T029 pendente: {pending.Length} unidades sem oraculos independentes completos ou com fonte impedida.");
    }
    [Fact]
    public void SourceRevisionAndAllPagesRecordsAndFieldsAreAccountedFor()
    {
        var pdf=File.ReadAllBytes(PathFor("docs/referencias/dimp-v10-manual-confaz.pdf"));
        Assert.Equal(RuleCatalog.SourceSha256,Convert.ToHexString(SHA256.HashData(pdf)));
        using var m=JsonDocument.Parse(File.ReadAllBytes(PathFor("docs/rules/dimp-v10/manifest.json")));
        var c=RuleCatalog.Load(File.ReadAllBytes(PathFor("docs/rules/dimp-v10/catalog.json")),m.RootElement.GetProperty("catalogSha256").GetString()!);
        using var source=JsonDocument.Parse(File.ReadAllBytes(PathFor("docs/rules/dimp-v10/source-units.json")));
        var pages=source.RootElement.GetProperty("units").EnumerateArray().Select(u=>u.GetProperty("page").GetInt32()).Distinct().Order().ToArray();
        Assert.Equal(Enumerable.Range(1,58),pages);
        var fields=source.RootElement.GetProperty("fields").EnumerateArray().ToArray();
        Assert.Equal(199,fields.Length);
        int[] counts=[10,2,4,9,4,14,5,4,8,2,10,11,14,2,2,2,9,6,12,14,15,7,12,10,2,2,3,2,2];
        for(var i=0;i<RuleCatalog.RecordCodes.Length;i++)
        {
            var code=RuleCatalog.RecordCodes[i];
            Assert.Equal(Enumerable.Range(1,counts[i]),fields.Where(f=>f.GetProperty("record").GetString()==code).Select(f=>f.GetProperty("position").GetInt32()));
        }
        foreach(var unit in source.RootElement.GetProperty("units").EnumerateArray())
        {
            var ids=unit.GetProperty("ruleIds").EnumerateArray().Select(x=>x.GetString()!).ToArray();
            Assert.True(ids.Length>0 || !string.IsNullOrWhiteSpace(unit.GetProperty("reason").GetString()));
            foreach(var id in ids)
            {
                var rule=c.Get(id);
                Assert.True(rule.Source.Locator==unit.GetProperty("locator").GetString() || unit.GetProperty("kind").GetString()=="reference_table");
            }
        }
        // A source inventory is not evidence that any fiscal rule ran.
        Assert.Equal(0,c.ImplementationCoverage.Implemented);
    }
    [Fact]
    public void SyntheticFixturesHaveIndependentExpectedResultsAndMeasuredBytes()
    {
        using var m=JsonDocument.Parse(File.ReadAllBytes(PathFor("tests/fixtures/dimp-v10/expected/manifest.json")));
        Assert.Equal(JsonValueKind.Null,m.RootElement.GetProperty("productMaxBytes").ValueKind);
        foreach(var f in m.RootElement.GetProperty("cases").EnumerateArray())
        {
            var bytes=File.ReadAllBytes(PathFor("tests/fixtures/dimp-v10/"+f.GetProperty("path").GetString()));
            Assert.Equal(f.GetProperty("bytes").GetInt32(),bytes.Length);
            Assert.Equal(f.GetProperty("sha256").GetString(),Convert.ToHexString(SHA256.HashData(bytes)));
            Assert.Equal("not_claimed",f.GetProperty("fiscalConformity").GetString());
        }
        var zero=System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(PathFor("tests/fixtures/dimp-v10/zerado-sintetico.dimp")));
        var rows=zero.Split("\r\n",StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(19,rows.Length);Assert.Contains("|0990|4|",rows);Assert.Contains("|1990|2|",rows);Assert.Contains("|9990|13|",rows);Assert.Contains("|9999|19|",rows);
        Assert.Equal(10,rows.Count(s=>s.StartsWith("|9900|",StringComparison.Ordinal)));
    }
}
