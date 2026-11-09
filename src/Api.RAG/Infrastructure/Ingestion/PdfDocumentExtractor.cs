using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Api.RAG.Infrastructure.Ingestion;

public sealed record ExtractedWord(string Text, double X, double Y, double Width, double Height);

public sealed record ExtractedLine(string Text, double Top, double Bottom);

public sealed record ExtractedPage(int PageNumber, string Text, IReadOnlyList<ExtractedLine> Lines);

public sealed record ExtractedPdfDocument(
    string Sha256,
    long ByteSize,
    int PageCount,
    IReadOnlyList<ExtractedPage> Pages);

public sealed class PdfDocumentExtractor
{
    public ExtractedPdfDocument Extract(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var byteSize = bytes.Length;

        using var document = PdfDocument.Open(bytes);
        var pageCount = document.NumberOfPages;
        var extractedPages = new List<ExtractedPage>(pageCount);

        for (var pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            var page = document.GetPage(pageNum);
            var words = page.GetWords().ToList();

            // Agrupa palavras por proximidade de linha vertical (coordenada Y decrescente, de cima para baixo no PDF)
            // No PdfPig, Y cresce de baixo para cima (BoundingBox.Bottom e Top)
            var lines = GroupWordsIntoLines(words);
            var pageTextBuilder = new StringBuilder();
            foreach (var line in lines)
            {
                pageTextBuilder.AppendLine(line.Text);
            }

            extractedPages.Add(new ExtractedPage(
                PageNumber: pageNum,
                Text: pageTextBuilder.ToString().TrimEnd(),
                Lines: lines));
        }

        return new ExtractedPdfDocument(
            Sha256: hash,
            ByteSize: byteSize,
            PageCount: pageCount,
            Pages: extractedPages);
    }

    private static IReadOnlyList<ExtractedLine> GroupWordsIntoLines(IReadOnlyList<Word> words)
    {
        if (words.Count == 0) return [];

        // Ordena verticalmente descrescente (topo para base) e depois horizontalmente (esquerda para direita)
        var orderedWords = words
            .OrderByDescending(w => w.BoundingBox.Top)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var lines = new List<List<Word>>();
        List<Word>? currentLine = null;
        double? currentLineY = null;

        foreach (var word in orderedWords)
        {
            var wordY = word.BoundingBox.Bottom;
            var wordHeight = word.BoundingBox.Height;
            var tolerance = Math.Max(2.0, wordHeight * 0.4);

            if (currentLine == null || currentLineY == null || Math.Abs(currentLineY.Value - wordY) > tolerance)
            {
                currentLine = [word];
                lines.Add(currentLine);
                currentLineY = wordY;
            }
            else
            {
                currentLine.Add(word);
            }
        }

        var result = new List<ExtractedLine>(lines.Count);
        foreach (var lineWords in lines)
        {
            var sortedLineWords = lineWords.OrderBy(w => w.BoundingBox.Left).ToList();
            var lineText = string.Join(" ", sortedLineWords.Select(w => w.Text.Trim())).Trim();
            if (!string.IsNullOrWhiteSpace(lineText))
            {
                var top = sortedLineWords.Max(w => w.BoundingBox.Top);
                var bottom = sortedLineWords.Min(w => w.BoundingBox.Bottom);
                result.Add(new ExtractedLine(lineText, top, bottom));
            }
        }

        return result;
    }
}
