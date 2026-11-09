using Api.RAG.Application.Ingestion;
using Api.RAG.Infrastructure.Ingestion;
using Xunit;

namespace Api.RAG.UnitTests;

public class DocumentChunkerTests
{
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AgenteDIMP.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz nao encontrada.");
    }

    [Fact]
    public void ChunkDocument_WithOfficialManual_IdentifiesRecordsAndSections()
    {
        var pdfPath = Path.Combine(FindRepositoryRoot(), "docs/referencias/dimp-v10-manual-confaz.pdf");
        if (!File.Exists(pdfPath)) return;

        using var stream = File.OpenRead(pdfPath);
        var extractor = new PdfDocumentExtractor();
        var extracted = extractor.Extract(stream);

        var chunker = new DocumentChunker();
        var profile = new ChunkProfile("chunk-layout10-section-record-v1", TargetMaxChars: 1000);
        var chunks = chunker.ChunkDocument(extracted, profile);

        Assert.NotEmpty(chunks);

        var records = chunks.Select(c => c.RecordCode).Where(r => r != null).Distinct().ToList();
        Assert.Contains("0000", records);
        Assert.Contains("0100", records);

        // Verifica que todos os chunks possuem hash deterministico e ordem sequencial
        for (var i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].OrderIndex);
            Assert.False(string.IsNullOrWhiteSpace(chunks[i].ChunkHash));
            Assert.NotEmpty(chunks[i].Pages);
        }
    }
}
