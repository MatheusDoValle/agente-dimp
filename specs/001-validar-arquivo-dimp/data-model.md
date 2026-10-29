# Modelo de dados e ciclo de vida

Data: 2026-10-29. Modelo lógico; tabelas, classes e migrações ainda não foram criadas.
Datas persistidas em UTC, cálculos por instante; apresentação pode usar America/Sao_Paulo.
Identificadores de execução e chamadas são UUIDs gerados pelo sistema.

## Propriedade e armazenamento

| Responsável | Armazenamento | Conteúdo |
| --- | --- | --- |
| Api.Agente | Banco agente em PostgreSQL | Execuções, fila, achados mascarados, relatórios, consumo e preços. |
| Api.Agente | Volume privado temporário | Original, índices SQLite temporários e mapa reversível apenas enquanto a execução estiver ativa. |
| Api.Agente | MongoDB Atlas | Memória/contexto limitado e já mascarado da geração; não é fila nem fonte oficial. |
| Api.RAG | Banco rag em PostgreSQL/pgvector | Documentos oficiais, revisões, chunks, perfis e vetores. |

Papéis de banco distintos; nenhum papel da Api.Agente consulta tabelas da Api.RAG.
Mascaramento não substitui isolamento por autorização; EX-001 continua aplicável.
O contexto local de desenvolvimento não é prova de identidade do usuário.

## Entidades da Api.Agente

### Execution

- id, correlationId, createdAt, startedAt, endedAt anulável, updatedAt.
- operatorId, contextId e identityAssurance=unverified_local_configuration obtidos
  da configuração do host e congelados ao aceitar a execução. Identificação local
  não autenticada; EX-001 não dispensa estes campos de rastreabilidade.
- status: recebida, em_processamento, concluida, concluida_parcialmente, falhou,
  cancelada. Estado terminal não equivale à conformidade.
- stage: recebimento, identificacao, validacao, recuperacao, analise,
  composicao, limpeza; não retornar percentual calculado sem base.
- layoutVersion, documentRevisionSha256, ruleCatalogVersion, configurationSnapshotId.
- bytesReceived, originalSha256 e nome de exibição sanitizado, se necessário.
- verdict: sem_inconsistencias_nas_regras_avaliadas, com_inconsistencias,
  inconclusivo, nao_suportado; não é uma certificação fiscal.
- coverage: applicable, passed, failed, notExecuted, blockedBySourceConflict.
- requiresHumanReview, limitationCodes, reportExpiresAt.
- technicalFailureCode e mensagem sanitizada; nunca stack trace/conteúdo bruto.

Relaciona um arquivo transitório, um trabalho, várias etapas, achados e chamadas.
O relatório pode ser parcial se validadores produziram resultados antes da falha de IA/RAG.

### ProcessingJob e StageCheckpoint

- executionId, availableAt, leaseOwner, leaseExpiresAt, heartbeatAt,
  fencingToken, attemptNumber e checkpointVersion.
- reservation status interno: pending, leased, completing, completed.
- checkpoint contém referências locais e progresso verificável, nunca presume resposta
  externa que não foi persistida.
- Chave única por executionId. Fencing token impede conclusão por lease antigo.
- Entrega pelo menos uma vez; etapa que reiniciar recompõe resultados de forma idempotente.
- Índice de status/availableAt/leaseExpiresAt para reserva com SKIP LOCKED.

### TemporaryArtifact

- executionId, artifactId, tipo, caminho interno gerado, createdAt, deletedAt,
  cleanupState e conteúdo criptográfico mínimo para conferir integridade.
- Tipos: original, parser_index, mask_map, upload_partial.
- Caminhos não são aceitos do cliente nem expostos em endpoints.
- Índices SQLite e seus sidecars ficam na mesma área de exclusão do original;
  biblioteca adicional justificada em ADR-0001 para referências/totalizadores sem RAM ilimitada.
- Não incluir diretório em backups e não copiar conteúdo para logs/histórico.

### RuleDefinition, RuleEvaluation e Finding

- RuleDefinition usa a identidade do catálogo definida em rule-catalog.md;
  versão do catálogo é imutável para a execução.
- RuleEvaluation: executionId, ruleId, contexto/ocorrência estável, applicability,
  result, location e evidenceIds.
- result: passed, failed, not_applicable, not_executed, blocked_by_source_conflict.
- Finding: id gerado pelo sistema, ruleId anulável para indício, categoria, severidade,
  descrição sanitizada, location, evidenceIds e origin.
- Categoria: erro_estrutural, inconsistencia_negocio, indicio_ia,
  ausencia_evidencias, falha_tecnica.
- Severidade: bloqueante (impede avaliar etapas dependentes), erro (violação comprovada),
  aviso (indício ou limitação para revisão), informativo (contexto).
- Ausência de evidência/falha técnica não vira erro de negócio por mudar severidade.
  Mapeamento por regra é explícito e revisável; o modelo não altera achados determinísticos.
- Location: linha baseada em 1, offset de bytes quando conhecido, código de registro,
  posição do campo e contexto pai; não persistir valor original.
- Chave idempotente por execução/regra/ocorrência. Achados paginados com ordem estável.

### Report e EvidenceSnapshot

- Report: executionId único, contractVersion, summary, analyticContent,
  requiresHumanReview, limitationCodes, createdAt, expiresAt.
- expiresAt = endedAt + 30 dias; não renovar ao consultar.
- EvidenceSnapshot: id, sourceKind, manualRevisionSha256, chunkId/regra,
  páginas/localizador, trecho do manual ou trecho mascarado do DIMP.
- Todas as mensagens derivadas dos validadores passam pelo filtro de mascaramento
  antes da persistência, inclusive descrições e parâmetros interpolados.
- Evidências do manual e do arquivo são tipos distintos.
- Reports/achados/evidências de execução são excluídos com a retenção do relatório.
  A existência da revisão documental permanece garantida durante a retenção dos resultados.

### ProviderCallAttempt e PriceBookVersion

- attemptId, logicalCallId, executionId anulável só para ingestão de acervo,
  ingestionJobId quando aplicável, serviceOwner e operationType.
- provider, requestedModel, returnedModel anulável, attemptNumber, reason,
  start/end, status e providerRequestId quando disponibilizado.
- tokensInput, tokensOutput, cachedTokens e embeddingTokens são anuláveis.
  Campo ausente não é gravado como zero; usageState: reported, partial, unknown.
- estimatedCost anulável, currency, priceBookVersionId, estimateStatus.
- PriceBookVersion: fonte, data de consulta, período de vigência, moeda, unidade e
  tarifas por categoria. Tarifa ausente produz custo desconhecido, nunca zero.
- Registro pré-chamada garante rastreio de tentativa; reconciliação após resposta.
  Falha no ledger impede iniciar nova chamada e não apaga resultados determinísticos.
- Retenção operacional de tentativas: 90 dias após a chamada, sem conteúdo bruto.
  Pricebook referenciado permanece enquanto houver tentativas que o utilizem.
- Api.RAG mantém seu ledger de embeddings; expõe resumo correlacionável por contrato.
  Api.Agente não lê tabelas rag nem duplica custo como se fossem duas chamadas.

### ExecutionTombstone

- executionId, expiredAt e purgeAt; sem relatório, nome, linhas, CPF/CNPJ ou payload.
- Mantido por 90 dias após expiração; durante esse prazo consulta retorna 410.
  Após purgeAt o identificador pode retornar 404.
- Essa retenção mínima de índice atende FR-032 e não prolonga o relatório.

## MongoDB Atlas: memória e contexto

A coleção conversation_contexts pertence à Api.Agente.
Documentos contêm id, executionId, conversationId, userId/organizationId quando
disponíveis, messages mascaradas, createdAt, updatedAt, expiresAt e versão do contexto.
A identidade é fornecida pelo sistema, não pelo modelo. Durante EX-001 o contexto
é exclusivamente da execução local; não introduzir conversação pública ou compartilhar
contexto entre execuções.

Guardar somente instruções de aplicação, resumos mascarados, IDs de achados e
referências; nunca o arquivo original ou mapa reversível.
Índice único de executionId/conversationId impede contextos duplicados.
Índice TTL de campo único em expiresAt usa expireAfterSeconds=0.
expiresAt acompanha o deadline finito da execução e toda leitura verifica sua validade.

TTL executa limpeza em segundo plano e não prova exclusão imediata.
A finalização deve remover explicitamente os documentos da execução e confirmar a
operação antes de publicar estado terminal. Recuperação trata contextos órfãos de
execuções encerradas; o banco não é fonte única da fila ou dos resultados oficiais.
Recuperação usa dados seguros de PostgreSQL e o temporário ativo.
A memória não fica retida por 30 dias automaticamente: esse prazo continua exclusivo
do relatório mascarado, segundo a escolha já registrada.

Usar MongoDB.Driver com MongoClient compartilhado por aplicação.
ConnectionStrings__MongoDb e MongoDb__DatabaseName são configuração externa;
MongoDB Atlas usa conexão TLS e database separado por ambiente.
Testes automatizados usam instância MongoDB local isolada com os mesmos índices
e contrato; nenhuma criação de conta/cluster ou conexão real é feita pelo plano.

## Entidades da Api.RAG

- Document: id e identificação lógica da fonte oficial.
- DocumentRevision: id, documentId, sourceUrl, SHA-256 único, layoutVersion,
  retrievedAt, byteSize, pageCount, sourceStatus e original oficial preservado.
  Atualização cria revisão; não sobrescreve revisão referenciada.
- IngestionJob: id, revisão, status, extractionProfile, chunkProfile, embeddingProfile,
  falhas sanitizadas e consumo correlacionado; chave única por revisão/hash + perfis.
- IngestionWorkItem: ingestionJobId único, availableAt, leaseOwner, leaseExpiresAt,
  heartbeatAt, fencingToken e checkpointVersion. Reserva transacional curta e
  fencing em toda publicação; não reutilizar ProcessingJob vinculado a executionId.
- IngestionChunkCheckpoint: ingestionJobId, chunkHash, embeddingProfileId,
  callAttemptId, estado pending/calling/persisted/unknown e vetor quando persistido.
  Chunks persistidos são reutilizados. Resposta externa perdida antes da gravação
  deixa tentativa unknown; não repetir automaticamente uma chamada ambígua.
  Nova tentativa exige decisão operacional explícita, com possível cobrança adicional
  registrada; nenhuma alegação de exactly-once perante o provedor.
- Publicar a revisão/perfis somente quando todos os chunks estiverem confirmados.
  Duplicata concorrente referencia o job existente; lease expirado retoma checkpoints,
  sem publicar corpus parcial nem cobrar novamente por resultado já persistido.
- KnowledgeChunk: id, revisionId, texto, páginas, seção, registros/campos associados
  quando recuperáveis, chunkHash e ordem.
- EmbeddingProfile: provedor, modelo, dimensão, normalização, métrica e versão.
- ChunkVector: chunkId, embeddingProfileId e vetor com dimensão compatível.
- RetrievalEvaluation: perfil, conjunto de referência, medidas e decisão de promoção.
- ProviderCallAttempt local: mesma semântica de consumo, propriedade da Api.RAG.

Unique em revisionHash + perfis + chunkHash evita duplicação por reingestão.
Índices comuns em revisão/layout/registro; busca vetorial exata sobre candidatos filtrados.
Outro perfil de dimensão ou revisão não participa da consulta.
Documento oficial e chunks são acervo, não arquivo DIMP submetido: sua retenção é
versionada por administração e não expira automaticamente com um relatório.

## Semântica dos contadores de execução

coverage conta instâncias de avaliação, identificadas por (ruleId, contexto/ocorrência),
e não regras distintas do catálogo. Cada instância tem exatamente um resultado.
not_applicable não entra em applicable nem nos demais contadores.
applicable = passed + failed + notExecuted + blockedBySourceConflict.
blockedBySourceConflict e notExecuted são categorias exclusivas; se não foi possível
determinar aplicabilidade de uma regra potencialmente obrigatória, registrar notExecuted
com limitação, em vez de considerá-la não aplicável.
Exemplo: a mesma regra aplicada a dois registros, um válido e outro inválido, produz
applicable=2, passed=1 e failed=1. Cobertura de implementação do catálogo continua
uma métrica separada por ruleId distinto, conforme rule-catalog.md.

## Estados, encerramento e limpeza

recebida → em_processamento → concluida / concluida_parcialmente / falhou / cancelada.

- recebida: arquivo íntegro e job confirmados, aguardando worker.
- em_processamento: inclui etapa limpeza e recuperação após interrupção.
- concluida: fluxo aplicável terminou; pode ter erros comprovados no arquivo.
- concluida_parcialmente: resultados disponíveis, mas uma etapa/regra não foi concluída.
- falhou: impossibilidade técnica sem relatório útil; não é erro fiscal.
- cancelada: término por cancelamento do sistema, com limpeza confirmada.
- Documento não suportado pode gerar relatório de resultado nao_suportado;
  não é automaticamente classificado como falha de infraestrutura.
- Só gravar endedAt depois de confirmar exclusão dos artefatos transitórios.
  Falha de limpeza mantém etapa limpeza com alerta operacional e impede falso encerramento.
- O orçamento global interrompe trabalho analítico e inicia finalização; não dispensa limpeza.
- Worker/coletor recuperam uploads órfãos e leases expirados de modo idempotente.

Na leitura: agora < expiresAt retorna conteúdo; agora >= expiresAt retorna 410.
Coletor remove payload expirado em lotes. Frequência inicial de manutenção: 1 minuto,
configurável; testes distinguem expiração lógica imediata da exclusão física posterior.
Logs sanitizados de desenvolvimento: 7 dias, sem corpos, prompts, nomes ou trechos brutos.

## Invariantes e validações

1. Exatamente uma identidade de execução por envio aceito; reenvio não sobrescreve.
2. Regra faltante/impedida ou dependência obrigatória incompleta impede conformidade.
3. LLM não escreve IDs operacionais, estado, custo ou contagem oficial de cobertura.
4. Toda citação aponta revisão/chunk/regra existente da execução.
5. Nem relatório, ledger, MongoDB ou logs retêm identificadores originais por atalho.
6. Reprocessamento após estado terminal exige novo envio do arquivo.
7. Exclusão de relatório não apaga métricas antes da política operacional própria,
   e métricas não conservam conteúdo que reconstrua o relatório.