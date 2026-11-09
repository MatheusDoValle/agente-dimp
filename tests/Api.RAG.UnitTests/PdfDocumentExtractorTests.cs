using Api.RAG.Infrastructure.Ingestion;
using Xunit;

namespace Api.RAG.UnitTests;

public class PdfDocumentExtractorTests
{
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AgenteDIMP.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz nao encontrada.");
    }

    [Fact]
    public void Extract_WithOfficialDimpManual_ExtractsPagesAndLines()
    {
        var pdfPath = Path.Combine(FindRepositoryRoot(), "docs/referencias/dimp-v10-manual-confaz.pdf");
        if (!File.Exists(pdfPath)) return;

        using var stream = File.OpenRead(pdfPath);
        var extractor = new PdfDocumentExtractor();
        var extracted = extractor.Extract(stream);

        Assert.Equal("fe5610af2429c718b59fffd18becba40c9c0697cbf02da6a3236465fa994ed0a", extracted.Sha256);
        Assert.True(extracted.PageCount >= 25);
        Assert.True(extracted.ByteSize > 500000);
        Assert.NotEmpty(extracted.Pages);

        var firstPage = extracted.Pages[0];
        Assert.Equal(1, firstPage.PageNumber);
        Assert.NotEmpty(firstPage.Lines);
        Assert.Contains("DIMP", firstPage.Text, StringComparison.OrdinalIgnoreCase);
    }
}
