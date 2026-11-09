using System.Collections.Immutable;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Dimp;
using Xunit;

namespace Api.Agente.UnitTests.Validation;

public class TemporaryValidationIndexTests
{
    [Fact]
    public async Task IndexesRecordsAndCalculatesAggregatesAndCleansUp()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"dimp_test_{Guid.NewGuid():N}.sqlite");

        await using (var index = await TemporaryValidationIndex.CreateAsync(tempFile))
        {
            var records = new List<DimpRecord>
            {
                new(1, 0, ["0000", "10", "4"], true, true, DimpLineEnding.CrLf, []),
                new(2, 20, ["1100", "ITEM1", "100,50"], true, true, DimpLineEnding.CrLf, []),
                new(3, 50, ["1100", "ITEM2", "200,25"], true, true, DimpLineEnding.CrLf, []),
                new(4, 80, ["9999", "4"], true, true, DimpLineEnding.CrLf, [])
            }.ToAsyncEnumerable();

            await index.PopulateAsync(records);

            Assert.Equal(1, await index.GetRecordCountAsync("0000"));
            Assert.Equal(2, await index.GetRecordCountAsync("1100"));
            Assert.Equal(1, await index.GetRecordCountAsync("9999"));
            Assert.Equal(0, await index.GetRecordCountAsync("0001"));
            Assert.Equal(4, await index.GetTotalRecordCountAsync());

            var sum = await index.SumDecimalFieldAsync("1100", 3);
            Assert.Equal(300.75m, sum);

            var items = await index.GetRecordsAsync("1100");
            Assert.Equal(2, items.Count);
            Assert.Equal("ITEM1", items[0].Fields[1]);
            Assert.Equal("ITEM2", items[1].Fields[1]);
        }

        // File should be cleaned up on DisposeAsync
        Assert.False(File.Exists(tempFile));
        Assert.False(File.Exists(tempFile + "-wal"));
        Assert.False(File.Exists(tempFile + "-shm"));
    }
}
