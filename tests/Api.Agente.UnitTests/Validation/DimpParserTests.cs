using System.Collections.Immutable;
using System.Text;
using Api.Agente.Domain.Validation;
using Xunit;

namespace Api.Agente.UnitTests.Validation;

public class DimpParserTests
{
    // Resolves the production type rather than substituting a test parser. Fails
    // explicitly until T031/T032; no mocked parser can make acceptance green.
    private static T Production<T>(string name) where T:class
    {
        var type=typeof(IDimpParser).Assembly.GetType(name);
        Assert.True(type is not null,$"T031/T032 pendente: {name}");
        return Assert.IsAssignableFrom<T>(Activator.CreateInstance(type!));
    }
    private static async Task<List<DimpRecord>> Parse(byte[] bytes)
    {
        using var stream=new FragmentedReadStream(bytes);
        var result=new List<DimpRecord>();
        await foreach(var record in Production<IDimpParser>("Api.Agente.Domain.Validation.DimpParser").ReadAsync(stream)) result.Add(record);
        return result;
    }
    [Fact]
    public async Task PreservesEmptyFieldsAndLatin1AcrossSplitCrLfWithoutGuessingEncoding()
    {
        var rows=await Parse(Encoding.Latin1.GetBytes("|0005|Ação||\r\n|0001|1|\r\n"));
        Assert.Equal(2,rows.Count);
        Assert.Equal(new[]{"0005","Ação",""},rows[0].Fields);
        Assert.Equal(1,rows[0].LineNumber);Assert.Equal(0,rows[0].ByteOffset);
        Assert.Equal(14,rows[1].ByteOffset);Assert.Equal(2,rows[1].LineNumber);
        Assert.All(rows,r=>Assert.Equal(DimpLineEnding.CrLf,r.LineEnding));
    }
    [Theory]
    [InlineData("|0001|1|\n",DimpLineEnding.Lf,true,true)]
    [InlineData("|0001|1|\r",DimpLineEnding.Cr,true,true)]
    [InlineData("|0001|1|",DimpLineEnding.None,true,true)]
    [InlineData("0001|1|\r\n",DimpLineEnding.CrLf,false,true)]
    [InlineData("|0001|1\r\n",DimpLineEnding.CrLf,true,false)]
    public async Task PreservesPhysicalViolationsInsteadOfRepairingThem(string input,DimpLineEnding ending,bool initial,bool final)
    {
        var row=Assert.Single(await Parse(Encoding.Latin1.GetBytes(input)));
        Assert.Equal(ending,row.LineEnding);Assert.Equal(initial,row.HasInitialPipe);Assert.Equal(final,row.HasFinalPipe);
    }
    [Fact]
    public async Task DoesNotTrimSpacesOrSkipBlankLines()
    {
        var rows=await Parse(Encoding.Latin1.GetBytes("|0001| 1 |\r\n\r\n|0990|3|\r\n"));
        Assert.Equal(3,rows.Count);Assert.Equal(" 1 ",rows[0].Fields[1]);Assert.Equal(2,rows[1].LineNumber);
    }
    [Fact]
    public async Task OrphanChildIsReportedWithoutInventingParent()
    {
        var rows=await Parse(File.ReadAllBytes(CatalogTestFiles.PathFor("tests/fixtures/dimp-v10/filho-orfao.dimp")));
        Assert.Contains(rows,r=>r.DiagnosticCodes.Contains("ORPHAN_RECORD"));
    }
    [Theory]
    [InlineData("09",false)][InlineData("10",true)][InlineData("99",false)]
    public void UnsupportedVersionDoesNotActivateV10Fallback(string version,bool supported)
    {
        var resolver=Production<IDimpLayoutResolver>("Api.Agente.Domain.Validation.LayoutResolver");
        var row=new DimpRecord(1,0,ImmutableArray.Create("0000",version,"4","SP","00000000000191","SINTETICA","20260601","20260630","2","202607"),true,true,DimpLineEnding.CrLf,[]);
        var result=resolver.Resolve(row);Assert.Equal(supported,result.Supported);
        if(supported)Assert.Equal(RuleCatalog.SourceSha256,result.RevisionSha256);
        else Assert.Null(result.RevisionSha256);
    }
    [Fact]
    public async Task PropagatesCancellation()
    {
        var parser=Production<IDimpParser>("Api.Agente.Domain.Validation.DimpParser");
        using var stream=new MemoryStream(Encoding.Latin1.GetBytes("|0001|1|\r\n"));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async ()=>
        { await foreach(var _ in parser.ReadAsync(stream,cancellation.Token)) { } });
    }
    private sealed class FragmentedReadStream(byte[] bytes):MemoryStream(bytes)
    {
        public override int Read(byte[] buffer,int offset,int count)=>base.Read(buffer,offset,Math.Min(1,count));
        public override int Read(Span<byte> buffer)=>base.Read(buffer[..Math.Min(1,buffer.Length)]);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)=>base.ReadAsync(buffer[..Math.Min(1,buffer.Length)],cancellationToken);
    }
}
