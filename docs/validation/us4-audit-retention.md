# Evidência de Validação - User Story 4 (US4) - Rastreabilidade, Consumo e Ciclo de Vida

**Data de Validação:** 09/11/2026  
**Ambiente:** .NET 10, PostgreSQL, MongoDB, Semantic Kernel, Model Context Protocol (MCP)  
**Status da Jornada:** Concluída (100% das tarefas T106–T117 implementadas e validadas)

---

## 1. Visão Geral da Entrega

A User Story 4 (US4) implementa a rastreabilidade completa, auditoria operacional de consumo de tokens, cálculo transparente de custos e ciclo de vida estrito de retenção para o processamento de arquivos DIMP, sem reter o arquivo original ou expor dados fiscais:

1. **Ledger Proprietário Pré-Chamada:**
   - Cada serviço proprietário (`Api.Agente` e `Api.RAG`) gerencia sua própria tabela PostgreSQL `provider_call_attempts`.
   - Cada tentativa de chamada a modelos externos (chat completion ou embeddings) é persistida como `AttemptStatus.Calling` **antes** do envio pela rede via `ProviderCallAttemptStore.ExecuteRecordedAsync`.
   - Se o registro no banco de dados falhar, nenhuma requisição externa é disparada, garantindo integridade auditável sem chamadas fantasmas.
   - Reconciliação imediata após a resposta: persistência dos tokens reais (`tokensInput`, `tokensOutput`, `cachedTokens`, `embeddingTokens`), duração e `providerRequestId`.

2. **Princípio Fundamental: Nulo Nunca Vira Zero:**
   - Métricas não reportadas pelo provedor ou chamadas com falha permanecem estritamente como `null` e `UsageState.Unknown` ou `UsageState.Partial`.
   - Em nenhuma circunstância tokens ausentes são computados como `0`.
   - Ausência de tarifa no catálogo de preços resulta em `estimatedCost = null` e `estimateStatus = "unknown"`, jamais em custo zero fabricado.

3. **Catálogo de Preços Versionado (`PriceBookVersion` e `PriceBookCostEstimator`):**
   - Tabela `price_books` e `price_book_rates` no PostgreSQL com alta precisão decimal `(28, 12)`.
   - Suporte a tarifas diferenciadas para tokens de entrada regulares, tokens de entrada em cache, tokens de saída e embeddings.
   - Tarifas ausentes para o modelo utilizado produzem custo desconhecido.

4. **Política Unificada de Tentativas (`ProviderAttemptScope`):**
   - Controle de orçamento em nível de operação: máximo de 3 tentativas transitórias (`AttemptReason.TransientRetry`), 1 reparo semântico (`AttemptReason.SemanticRepair`) e teto global de 4 tentativas no total.
   - SDK configurado sem retries ocultos em segundo plano, registrando o número exato de requisições de transporte disparadas.

5. **Correlação entre Serviços via MCP sem Duplicação:**
   - A `Api.Agente` nunca lê o banco de dados `rag` diretamente.
   - O consumo de embeddings de busca e ingestão é informado pelo servidor MCP através de `UsageTools.cs` e `McpUsageReference`.
   - O `ExecutionUsageAggregator` correlaciona o consumo local de IA da execução com as referências MCP, garantindo deduplicação por `CallAttemptId` para que nenhuma chamada de embedding seja somada duas vezes.

6. **Ciclo de Vida, Expiração em 30 Dias e Tombstones de 90 Dias:**
   - Relatórios mascarados têm retenção finita de 30 dias a partir do encerramento (`endedAt + 30 dias`).
   - Expiração lógica imediata: ao completar 30 dias, a consulta via endpoint HTTP `/api/v1/analises/{id}/relatorio` retorna imediatamente `410 Gone`, independentemente da execução do coletor assíncrono.
   - `ReportExpirationService` e `RetentionWorker`: o coletor expira execuções com mais de 30 dias, removendo o conteúdo do relatório, achados, evidências e avaliações, gravando um registro mínimo `ExecutionTombstone` (`executionId`, `expiredAt`, `purgeAt = expiredAt + 90 dias`).
   - Durante os 90 dias de vida do tombstone, qualquer consulta ao identificador continua retornando `410 Gone`.
   - Após `purgeAt` (120 dias no total), o tombstone é expurgado definitivamente e consultas ao identificador passam a retornar `404 Not Found`.

7. **Limpeza e Recuperação de Recursos Órfãos:**
   - `ExecutionFinalizer` e `ArtifactRecoveryWorker` removem arquivos temporários do disco e limpam o contexto conversacional no MongoDB via exclusão explícita por `executionId`.
   - Jobs com lease expirado por queda do worker são recuperados ou marcados como falhos após exceder o limite de retentativas.

---

## 2. Testes de Unidade e Integração Executados

### 2.1 Testes de Ledger e Custos (`UsageAccountingTests.cs` - 12 testes)
- `UsageMeasurement_Validation_RejectsNegativeTokens`: APROVADO.
- `UsageMeasurement_Validation_RejectsCachedTokensExceedingInputTokens`: APROVADO.
- `ProviderCallAttempt_Complete_WithNullTokens_ResultsInUnknownUsageState_AndNullDoesNotTurnIntoZero`: APROVADO.
- `ProviderCallAttempt_Complete_WithPartialTokens_ResultsInPartialUsageState`: APROVADO.
- `ProviderCallAttempt_Complete_WithFullTokens_ResultsInReportedUsageState`: APROVADO.
- `ProviderCallAttempt_Fail_SetsUnknownOrCancelledStatus_AndRecordsDuration`: APROVADO.
- `PriceBookCostEstimator_WhenPriceBookUnavailable_ProducesUnknownCostAndNullNeverTurnsIntoZero`: APROVADO.
- `PriceBookCostEstimator_WhenRateMissingForModel_ProducesUnknownCostAndNullNeverTurnsIntoZero`: APROVADO.
- `PriceBookCostEstimator_WhenTokensAreNullOrPartial_ProducesUnknownCostAndNullNeverTurnsIntoZero`: APROVADO.
- `PriceBookCostEstimator_WhenCachedTokensPresentWithoutCachedRate_ProducesUnknownCost`: APROVADO.
- `PriceBookCostEstimator_CalculatesAccurateDecimalCost_WithFullRatesAndTokens`: APROVADO.
- `PriceBookCostEstimator_ForEmbedding_CalculatesAccurateDecimalCost`: APROVADO.

### 2.2 Testes de Agregação e Correlação (`ExecutionUsageAggregatorTests.cs` - 2 testes)
- `Aggregator_CombinesLocalAndMcpUsage_WithDeduplication_AndDecimalAccuracy`: APROVADO (deduplicação por `CallAttemptId`, cálculo de precisão decimal de custos somando tokens de chat e embedding).
- `Aggregator_WhenAnyAttemptHasUnknownTokens_SetsHasUnknownUsageTrue_AndNullCost`: APROVADO (preservação estrita de custo nulo em presença de métrica desconhecida).

### 2.3 Testes de Ciclo de Vida e Fronteira Temporal (`RetentionBoundaryTests.cs` e `ExecutionAuditTests.cs`)
- `Report_IsAccessibleBefore30Days_AndReturns410ImmediatelyAt30Days`: APROVADO (relatório 200 OK no dia 29 e 410 Gone no dia 30 + 1s).
- `ReportExpirationService_SweepsExpiredExecution_CreatesTombstone_AndPurgesAt90Days`: APROVADO (expurgo de relatório com geração de tombstone; 410 durante 90 dias e 404 após expurgo do tombstone no dia 120).
- `Execution_AuditFlow_WithAttemptsAndPriceBook_PublishesAccurateUsageInReport`: APROVADO (publicação do envelope `UsageSummary` com `inputTokens`, `outputTokens`, `hasUnknownUsage = false` e `estimatedCost = "0.02"`).
- `Execution_AuditFlow_WithUnknownUsageState_PropagatesHasUnknownUsageTrue`: APROVADO (preservação de `hasUnknownUsage = true` e `estimatedCost = null`).
