using System.Text;
using Api.Agente.Domain.Validation;
using Api.Agente.Infrastructure.Rag;

namespace Api.Agente.Application.AI;

public sealed record BuiltAnalysisPrompt(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<Guid> AllowedFindingIds,
    IReadOnlyList<Guid> AllowedEvidenceChunkIds);

public sealed class AnalysisContextBuilder
{
    public BuiltAnalysisPrompt BuildPrompt(
        Guid executionId,
        string layoutVersion,
        string manualRevisionSha256,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<McpHit> evidenceHits)
    {
        var allowedFindingIds = findings.Select(f => f.Id).ToList();
        var allowedEvidenceIds = evidenceHits.Select(e => e.ChunkId).ToList();

        var systemPrompt = """
            Você é um assistente especialista na auditoria fiscal e técnica da Declaração de Informações sobre Meios de Pagamentos (DIMP), conforme o Manual de Orientação do Leiaute da DIMP emitido pelo CONFAZ.
            Sua responsabilidade é analisar os achados determinísticos identificados pelo validador, confrontá-los com as evidências normativas do manual oficial e produzir uma síntese analítica estruturada em JSON.

            REGRAS OBRIGATÓRIAS DE INTEGRIDADE:
            1. Você JAMAIS pode alterar os fatos determinísticos, alterar a severidade ou anular achados do validador.
            2. Cada explicação ou indício deve referenciar exclusivamente os IDs de achados existentes (findingIds) e os IDs de evidências do manual oficial fornecidas (evidenceIds).
            3. Não invente IDs, regras ou trechos do manual que não constem no contexto.
            4. Se não houver evidências normativas suficientes, registre essa limitação no campo 'limitations'.
            5. Retorne a resposta exclusivamente no formato JSON estrito conforme o schema contratual, sem preâmbulos ou blocos Markdown adicionais.
            """;

        var sb = new StringBuilder();
        sb.AppendLine($"# EXECUÇÃO DA ANÁLISE: {executionId}");
        sb.AppendLine($"Leiaute DIMP: Versão {layoutVersion} | Revisão SHA-256 do Manual: {manualRevisionSha256}");
        sb.AppendLine();

        sb.AppendLine("## ACHADOS DETERMINÍSTICOS IDENTIFICADOS PELO SISTEMA:");
        if (findings.Count == 0)
        {
            sb.AppendLine("Nenhuma inconsistência encontrada nas regras determinísticas avaliadas.");
        }
        else
        {
            foreach (var f in findings)
            {
                var lineStr = f.Location.Line?.ToString() ?? "N/A";
                var fieldStr = f.Location.FieldPosition?.ToString() ?? "N/A";
                var recStr = f.Location.RecordCode ?? "N/A";
                sb.AppendLine($"- [findingId: {f.Id}] Regra: {f.RuleId ?? "N/A"} | Registro: {recStr} | Linha: {lineStr} | Campo: {fieldStr} | Severidade: {f.Severity} | Descrição: {f.Description}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## EVIDÊNCIAS NORMATIVAS RECUPERADAS DO MANUAL OFICIAL:");
        if (evidenceHits.Count == 0)
        {
            sb.AppendLine("Nenhuma evidência normativa textual associada.");
        }
        else
        {
            foreach (var hit in evidenceHits)
            {
                var pagesStr = hit.Pages.Length > 0 ? string.Join(", ", hit.Pages) : "N/A";
                sb.AppendLine($"- [evidenceId: {hit.ChunkId}] Seção: {hit.Section ?? "Geral"} (Páginas: {pagesStr}):");
                sb.AppendLine($"  \"{hit.Text}\"");
            }
        }
        sb.AppendLine();

        sb.AppendLine("Gere o resumo analítico explicando os achados à luz do manual, identificando se há necessidade de revisão humana.");

        return new BuiltAnalysisPrompt(
            SystemPrompt: systemPrompt,
            UserPrompt: sb.ToString(),
            AllowedFindingIds: allowedFindingIds,
            AllowedEvidenceChunkIds: allowedEvidenceIds);
    }
}
