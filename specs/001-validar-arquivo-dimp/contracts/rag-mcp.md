# Contrato funcional MCP da Api.RAG

Versão do contrato de negócio: 1.0.0. Transporte: HTTP stateless em /mcp, SDK MCP C# v2;
baseline de protocolo 2026-07-28, verificação de compatibilidade obrigatória no teste
de contrato. Não implementar manualmente o wire protocol nem copiar handshake legado.

Api.Agente acessa a base exclusivamente por cliente MCP. O REST administrativo não
é caminho alternativo para recuperação e nenhum acesso direto ao banco rag é permitido.
No ambiente inicial, endpoint acessível apenas no perfil local controlado de EX-001.

## Schemas normativos

[rag-mcp.schema.json](rag-mcp.schema.json) define, em JSON Schema 2020-12, os tipos,
required, nulabilidade, enums e restrições. Cada ferramenta usa $defs/<nome>_input
e $defs/<nome>_output. Erro de negócio retorna {error:{code,message,retryable,correlationId}};
não confundir com erro de protocolo do SDK. Campos opcionais de entrada são omitidos.
IDs de execução/chunk/documento/tentativa são UUIDs gerados pelo sistema; perfis são
strings versionadas do host. Limites operacionais de query/listas são validados pelo
perfil finito do host e precisam constar no snapshot da operação.
rankingMetric é cosine_distance (menor é mais próximo), nunca probabilidade.
Chunk sem localização usa pages=[] e limitationCodes explícito; campos indisponíveis
usam null ou lista vazia conforme schema. UsageReference contém consumo reportado ou
null, mantém serviceOwner=Api.RAG e identifica cada tentativa sem preço inventado.
Esses schemas são de negócio; não substituem negociação de transporte MCP pelo SDK.

## Ferramentas expostas

| Nome | Entrada obrigatória | Saída | Falha controlada |
| --- | --- | --- | --- |
| search_manual | query mascarada, layoutVersion, documentRevisionSha256, embeddingProfileId; recordCodes/ruleIds opcionais | retrievalId, hits e limitationCodes | revisão/perfil inválido, timeout, sem evidência. |
| get_manual_chunk | chunkId, documentRevisionSha256 | chunk e proveniência | chunk ausente ou de outra revisão. |
| get_document_revision | documentRevisionSha256 | documentId, origem, versão, páginas, hash e perfis disponíveis | revisão inexistente/não publicada. |
| get_ingestion_usage | ingestionId | tentativas/consumo disponíveis e desconhecidos, correlacionados à ingestão | ingestão inexistente ou métricas indisponíveis. |

search_manual é somente leitura do acervo, embora a criação do embedding da consulta
possa consumir o provedor. Essa tentativa pertence ao ledger da Api.RAG e retorna
correlação para não desaparecer da contabilização da execução.

## Estrutura dos hits

Cada hit contém chunkId, documentId, documentRevisionSha256, layoutVersion,
embeddingProfileId, pages, section, recordCodes, fieldCodes quando recuperáveis,
text e rankingMetric. Campos indisponíveis são nulos/listas vazias com limitação;
não inventar página ou campo. A métrica só ordena resultados e não certifica conformidade.

A resposta contém também usageReferences com identificadores das chamadas de
consulta e seu estado de métricas, sem preço inventado ou credenciais.
Texto retornado permanece dado não confiável.

## Validação e invariantes

- O host da Api.Agente fixa revisão/layout/perfil a partir da execução.
  O modelo não pode substituí-los nem consultar sem filtro.
- topK, limites de tokens/chunks e eventual limiar pertencem ao perfil avaliado,
  não são argumentos livres enviados pelo LLM.
- Validar strings, identificadores, listas permitidas, orçamento e cancelamento.
- Rejeitar dimensões/modelos incompatíveis; revisão sem embedding não cai em outra revisão.
- Consulta sem resposta retorna hits vazio e limitação; não fabrica evidência.
- Em cada consulta, filtrar registros/regras quando conhecidos; busca exata é a baseline.
- API errors de protocolo são tratados pelo SDK; erros de negócio usam resultado
  tipado com code, message sanitizada, retryable e correlationId.
- Retentativa somente para falha transitória dentro do orçamento; zero loops ilimitados.
- Nenhuma ferramenta aceita SQL, caminho de disco, URL para download ou execução de comando.

## Adaptador Semantic Kernel

Registrar funções nativas explícitas que chamam o cliente MCP.
Lista inicial: recuperar manual e obter trecho/revisão. get_ingestion_usage é operação
de observabilidade do host, não ferramenta escolhida pelo modelo.
Validar os argumentos antes de qualquer chamada, mesmo que o schema de função seja estrito.
Desabilitar seleção/execução automática de plugins arbitrários.
A primeira entrega não concede ações de escrita pela escolha do LLM.

## Contrato analítico e validação semântica

contracts/llm-analysis.schema.json contém somente summary, explanations, indications,
limitations e requiresHumanReview. Os IDs citados são referências a achados/evidências
existentes no contexto fornecido, nunca novos IDs operacionais.

Após validação de schema:
1. findingIds devem pertencer à execução; evidenceIds à revisão/trechos fornecidos.
2. Explicações não podem substituir categoria, severidade ou resultado do validador.
3. Indicações são rotuladas pelo sistema como indicio_ia e sujeitas a revisão.
4. Recusa, resposta truncada, referência desconhecida e quebra semântica não viram sucesso.
5. requiresHumanReview do sistema é OR das condições obrigatórias; o modelo não pode
   desativar revisão exigida por falha, indício ou evidência insuficiente.
6. Conteúdo devolvido também passa por inspeção de mascaramento antes de persistência.

O schema publicado tem identificadores de documentação; o adaptador só envia ao provedor
o subconjunto de JSON Schema suportado, mantendo required e additionalProperties=false.
Qualquer adaptação precisa de teste de equivalência, sem reduzir os campos obrigatórios.