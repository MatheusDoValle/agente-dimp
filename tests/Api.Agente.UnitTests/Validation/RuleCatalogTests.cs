using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Api.Agente.Domain.Validation;
using Xunit;

namespace Api.Agente.UnitTests.Validation;

public class RuleCatalogTests
{
    [Fact]
    public void ExecutionSourceCannotChangeAfterBindingIncludingDuringRetries()
    {
        var catalog=CatalogTestFiles.Load();var snapshotId=Guid.NewGuid();
        var execution=new Api.Agente.Domain.Execution(Guid.NewGuid(),Guid.NewGuid(),"operator-test","context-test",DateTimeOffset.UtcNow);
        catalog.BindToExecution(execution,snapshotId);catalog.BindToExecution(execution,snapshotId);
        Assert.Equal(catalog.RevisionSha256,execution.DocumentRevisionSha256);
        Assert.Equal(catalog.Version,execution.RuleCatalogVersion);
        Assert.Throws<InvalidOperationException>(()=>execution.SetSource("10",new string('A',64),catalog.Version,snapshotId));
        Assert.Throws<InvalidOperationException>(()=>catalog.BindToExecution(execution,Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(()=>execution.SetSource("10",catalog.RevisionSha256,"other-version",snapshotId));
    }
    [Fact]
    public void PinsRevisionAndSeparatesSoftwareCoverageFromExecutionInstances()
    {
        var c=CatalogTestFiles.Load();
        Assert.Equal(RuleCatalog.SourceSha256,c.RevisionSha256);
        Assert.Equal(RuleCatalog.SupportedVersion,c.Version);
        Assert.Equal(29,c.Rules.Where(r=>r.Record is not null).Select(r=>r.Record).Distinct().Count());
        Assert.Equal(0,c.ImplementationCoverage.Implemented);
        Assert.Equal(c.Rules.Length,c.ImplementationCoverage.Planned+c.ImplementationCoverage.Blocked);
        var id=Guid.NewGuid();
        var rows=new[] {c.CreateUnavailableEvaluation(id,"DIMP10.FILE.CRLF","line:1"),c.CreateUnavailableEvaluation(id,"DIMP10.FILE.CRLF","line:2")};
        Assert.Equal(2,CoverageTotals.FromEvaluations(rows).NotExecuted);
        Assert.All(rows,r=>Assert.Equal(RuleApplicability.Undetermined,r.Applicability));
    }
    [Fact]
    public void RejectsTamperedBytesAndDoesNotGuessRevision()
    {
        var bytes=CatalogTestFiles.Bytes;
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        var changed=bytes.Concat(new byte[]{32}).ToArray();
        Assert.Throws<InvalidDataException>(()=>RuleCatalog.Load(changed,hash));
        Assert.Throws<InvalidDataException>(()=>RuleCatalog.Load(bytes,hash,expectedRevisionSha256:new string('A',64)));
        Assert.Throws<InvalidDataException>(()=>RuleCatalog.Load(bytes,hash,expectedVersion:"future"));
    }
    [Theory]
    [InlineData("duplicate")]
    [InlineData("page")]
    [InlineData("missing_required")]
    [InlineData("unknown_property")]
    [InlineData("wrong_revision")]
    [InlineData("conflict_implemented")]
    [InlineData("missing_dependency")]
    [InlineData("dependency_cycle")]
    [InlineData("no_positive_test")]
    public void RejectsBrokenProvenanceOrFalseImplementationClaim(string mutation)
    {
        var root=JsonNode.Parse(CatalogTestFiles.Bytes)!.AsObject();
        var rows=root["rules"]!.AsArray();var first=rows[0]!.AsObject();
        switch(mutation)
        {
            case "duplicate":rows.Add(first.DeepClone());break;
            case "page":first["source"]!["page"]=0;break;
            case "missing_required":first.Remove("applicability");break;
            case "unknown_property":first["executeArbitraryCode"]=true;break;
            case "wrong_revision":first["revisionSha256"]=new string('A',64);break;
            case "conflict_implemented":
                first["interpretation"]="conflicting";first["conflictIds"]=new JsonArray("SF-001");first["implementation"]="implemented";break;
            case "missing_dependency":first["dependencies"]=new JsonArray("DIMP10.UNKNOWN");break;
            case "dependency_cycle":first["dependencies"]=new JsonArray(first["id"]!.GetValue<string>());break;
            case "no_positive_test":first["implementation"]="implemented";break;
        }
        var bytes=Encoding.UTF8.GetBytes(root.ToJsonString());
        Assert.Throws<InvalidDataException>(()=>RuleCatalog.Load(bytes,Convert.ToHexString(SHA256.HashData(bytes))));
    }
    [Theory]
    [InlineData("SF-001")][InlineData("SF-002")][InlineData("SF-003")]
    [InlineData("SF-004")][InlineData("SF-005")][InlineData("SF-006")]
    [InlineData("SF-007")][InlineData("SF-008")][InlineData("SF-009")]
    [InlineData("SF-010")]
    public void UnresolvedSourceNeverPassesAndCannotBecomeCustomerError(string conflict)
    {
        var catalog=CatalogTestFiles.Load();var id=Guid.NewGuid();
        var affected=catalog.Rules.Where(r=>r.ConflictIds.Contains(conflict)).ToArray();
        Assert.NotEmpty(affected);
        Assert.All(affected,rule=>
        {
            Assert.Equal("blocked",rule.Implementation);Assert.Null(rule.Resolution);
            Assert.Equal(RuleResult.BlockedBySourceConflict,catalog.CreateUnavailableEvaluation(id,rule.Id,"file").Result);
            Assert.All(rule.Tests,t=>Assert.NotEqual("implemented",t.Status));
        });
    }
}
