using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Api.RAG.Domain.Knowledge;
using Api.RAG.Infrastructure.Ingestion;

namespace Api.RAG.Application.Ingestion;

public sealed record ChunkProfile(
    string ProfileId,
    int TargetMaxChars = 1200,
    int MaxChars = 1800,
    int OverlapChars = 100);

public sealed record ChunkedItem(
    string Text,
    int[] Pages,
    string Section,
    string? RecordCode,
    string? FieldCode,
    string ChunkHash,
    int OrderIndex);

public sealed partial class DocumentChunker
{
    private static readonly Regex RecordHeaderRegex = GeneratedRecordRegex();
    private static readonly Regex SectionHeaderRegex = GeneratedSectionRegex();
    private static readonly Regex FieldRegex = GeneratedFieldRegex();

    [GeneratedRegex(@"\bREGISTRO\s+([0-9]{4})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GeneratedRecordRegex();

    [GeneratedRegex(@"^([0-9]+(\.[0-9]+)*)\s+([A-ZÁÉÍÓÚÂÊÔÃÕÇ\s–\-]+)", RegexOptions.Compiled)]
    private static partial Regex GeneratedSectionRegex();

    [GeneratedRegex(@"\b(?:CAMPO|ITEM)\s+([0-9]{1,3})\s*[-–:]\s*([A-Za-z0-9_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GeneratedFieldRegex();

    public IReadOnlyList<ChunkedItem> ChunkDocument(ExtractedPdfDocument document, ChunkProfile profile)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(profile);

        var chunks = new List<ChunkedItem>();
        var currentSection = "GERAL";
        string? currentRecordCode = null;
        var currentPages = new HashSet<int>();
        var currentText = new StringBuilder();
        string? currentFieldCode = null;
        var orderIndex = 0;

        void FlushChunk()
        {
            var text = currentText.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            var pages = currentPages.OrderBy(p => p).ToArray();
            var chunkHash = ComputeChunkHash(text, pages, currentSection, currentRecordCode);

            chunks.Add(new ChunkedItem(
                Text: text,
                Pages: pages,
                Section: currentSection,
                RecordCode: currentRecordCode,
                FieldCode: currentFieldCode,
                ChunkHash: chunkHash,
                OrderIndex: orderIndex++));

            currentText.Clear();
            currentPages.Clear();
        }

        foreach (var page in document.Pages)
        {
            foreach (var line in page.Lines)
            {
                var lineText = line.Text.Trim();
                if (string.IsNullOrWhiteSpace(lineText)) continue;

                // Detecta Registro DIMP (ex: REGISTRO 0000, REGISTRO 0100)
                var recordMatch = RecordHeaderRegex.Match(lineText);
                if (recordMatch.Success)
                {
                    FlushChunk();
                    currentRecordCode = recordMatch.Groups[1].Value;
                    currentSection = lineText;
                    currentFieldCode = null;
                }
                else
                {
                    // Detecta seções numéricas gerais (ex: 1. INTRODUÇÃO, 2. ESTRUTURA)
                    var sectionMatch = SectionHeaderRegex.Match(lineText);
                    if (sectionMatch.Success && lineText.Length < 80)
                    {
                        FlushChunk();
                        currentSection = lineText;
                        currentRecordCode = null;
                        currentFieldCode = null;
                    }
                }

                // Detecta campo (ex: CAMPO 02 - COD_VER)
                var fieldMatch = FieldRegex.Match(lineText);
                if (fieldMatch.Success)
                {
                    currentFieldCode = fieldMatch.Groups[2].Value;
                }

                currentPages.Add(page.PageNumber);
                currentText.AppendLine(lineText);

                if (currentText.Length >= profile.TargetMaxChars)
                {
                    FlushChunk();
                    // Se estivermos dentro de um registro, reinicia o próximo chunk com o contexto do cabeçalho
                    if (!string.IsNullOrWhiteSpace(currentRecordCode))
                    {
                        currentText.AppendLine($"[Contexto: REGISTRO {currentRecordCode} - {currentSection}]");
                        currentPages.Add(page.PageNumber);
                    }
                }
            }
        }

        FlushChunk();
        return chunks;
    }

    private static string ComputeChunkHash(string text, int[] pages, string section, string? recordCode)
    {
        var raw = $"{text}|{string.Join(",", pages)}|{section}|{recordCode}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
