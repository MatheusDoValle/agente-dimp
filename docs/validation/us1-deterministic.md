# Evidência de Validação Determinística - User Story 1 (US1)

**Data**: 2026-11-05  
**Ambiente**: .NET 10 (C# 14), PostgreSQL 17 com pgvector (Docker local), SQLite para índices temporários.  
**Branch**: `main`  
**Escopo**: Conclusão da User Story 1 (US1) - "Receber e validar um arquivo DIMP determinístico".

---

## 1. Resumo Executivo

A User Story 1 foi completamente implementada e validada através de uma suíte automatizada de testes cobrindo todo o ciclo de vida do arquivo DIMP V10: recepção streaming multipart, persistência transacional durável, fila assíncrona com leasing transacional curto (`SKIP LOCKED`), parsing com rastreamento de hierarquia, indexação temporária em SQLite, validação de todos os 29 registros e 199 campos, composição de relatório determinístico, endpoints de consulta e expurgo de artefatos temporários ao finalizar.

Todos os **207 testes automatizados** da solução estão passando com **0 falhas**.

---

## 2. Componentes Implementados na US1

### 2.1 Recepção e Streaming Multipart (T067)
- **Endpoint**: `POST /api/v1/analises` (`SubmitAnalysisEndpoint.cs`).
- **Validação de Entrada**:
  - Rejeição de content-type não-multipart (HTTP 415).
  - Rejeição de multipart malformado ou truncado (HTTP 400 - `INVALID_MULTIPART`).
  - Rejeição de payload vazio (HTTP 422 - `EMPTY_FILE`).
  - Limite de corpo excedido via Kestrel (HTTP 413 - `PAYLOAD_TOO_LARGE`).
  - Indisponibilidade de banco de dados retorna HTTP 503 (`PERSISTENCE_UNAVAILABLE`) sem vazar credenciais ou strings de conexão.
  - Sucesso retorna HTTP 202 (`Accepted`) com header `Location: /api/v1/analises/{id}` e body estruturado contendo `ExecutionId`, `StatusUrl` e `ReportUrl`.
- **Integridade**: Streaming direto para arquivo temporário com cálculo concomitante de tamanho em bytes e hash SHA-256.

### 2.2 Fila Durável e Repositório de Reserva (T066)
- **Tabela**: `agente.processing_jobs` (Migration EF Core `AddProcessingJobs`).
- **Modelo e Ciclo de Vida**:
  - Estados internos: `pending`, `leased`, `completing`, `completed`.
  - Controle de posse: `LeaseOwner`, `LeaseExpiresAt`, `HeartbeatAt`.
  - Proteção contra concorrência e split-brain: `FencingToken` monotônico incrementado a cada reserva.
  - Reserva transacional curta: `FOR UPDATE SKIP LOCKED`, garantindo que nenhuma transação permaneça aberta durante parsing ou validação.
  - Implementação: `PostgresJobStore.cs`.

### 2.3 Processamento Assíncrono e Motor de Validação (T068)
- **Worker**: `AnalysisWorker.cs` (`BackgroundService`).
- **Pipeline de Execução**:
  1. *Lease*: Reserva o job com timeout de 2 minutos.
  2. *Identificação*: Extrai registro `0000`, valida layout V10 via `LayoutResolver` e afere SHA-256 oficial do documento S01 (`FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A`).
  3. *Indexação*: Cria banco temporário SQLite (`TemporaryValidationIndex.cs`) para contagem de registros e totalizações com aritmética decimal exata via `decimal_fields`.
  4. *Parsing & Validação*: Executa `DimpParser` e `DimpValidationEngine` avaliando todos os 29 validadores de registro (`Record0000Validator` a `Record9999Validator`).
  5. *Privacidade & Mascaramento*: Achados e evidências são sanitizados via `ContentSanitizer` antes da gravação no `AgentDbContext`.
  6. *Composição*: Marca veredito (`SemInconsistenciasNasRegrasAvaliadas` ou `ComInconsistencias`) e indica se requer revisão humana.
  7. *Limpeza e Finalização*: Coordena com `ExecutionFinalizer` a exclusão física confirmada de todos os artefatos temporários antes de marcar `EndedAt` e concluir o job.

### 2.4 Endpoints de Consulta e Relatório (T069)
- **Consulta de Status**: `GET /api/v1/analises/{id}` (`GetAnalysisStatusEndpoint.cs`) - retorna `ExecutionStatus` com etapa, veredito, flags de relatório e limitação. Retorna 404 se inexistente e 410 se expirado.
- **Relatório Determinístico**: `GET /api/v1/analises/{id}/relatorio` (`GetAnalysisReportEndpoint.cs`) - retorna `Contracts.Report` estruturado conforme OpenAPI. Retorna 409 se ainda em andamento.
- **Construtor**: `DeterministicReportBuilder.cs`.

### 2.5 Resiliência e Recuperação (T070)
- Testes em `DurableJobRecoveryTests.cs`:
  - Múltiplos workers disputando jobs em paralelo recebem trabalhos distintos sem duplicação (`ConcurrentWorkersReceiveDistinctJobs`).
  - Worker cujo lease expira perde a posse e outro worker reassume com `FencingToken` incrementado, rejeitando tentativas antigas (`ExpiredLeaseCanBeReclaimedByAnotherWorker`).
  - Arquivo ausente ou corrompido é tratado graciosamente, transacionando a execução para falha sem travar o worker (`WorkerRecoversGracefullyWhenArtifactMissing`).
  - Desconexão do cliente HTTP após o recebimento do HTTP 202 não interrompe o processamento durável em background (`ClientDisconnectAfterAcceptanceDoesNotCancelDurableProcessing`).

---

## 3. Estado das Regras e Conflitos de Fonte (SF-001, SF-002, SF-003)

Em estrita conformidade com as diretrizes do projeto e os requisitos de integridade fiscal, as três regras oficiais conflitantes do CONFAZ mantêm-se como **impedimentos declarados**:

| Conflito | Registro | Campo | Descrição | Tratamento Técnico |
|---|---|---|---|---|
| **SF-001** | `0000` | 4 (`COD_VER`) | Finalidade 9 não prevista em S01 | Retorna `RuleResult.BlockedBySourceConflict`, `RuleApplicability.Undetermined`. |
| **SF-002** | `1120` | 14 (`COD_MUN_DEST`) | Regra conflitante entre texto e tabela em operações interestaduais | Retorna `RuleResult.BlockedBySourceConflict`, `RuleApplicability.Undetermined`. |
| **SF-003** | `1115` | 12 (`IND_TP_PIX`) | Regra de preenchimento conflitante com modalidade de liquidação | Retorna `RuleResult.BlockedBySourceConflict`, `RuleApplicability.Undetermined`. |

Nenhuma interpretação fictícia foi aplicada. O gate `EverySourceRuleRequiresIndependentOraclesBeforeFiscalCoverageCanBeAccepted` permanece ativo sob o trait `Category=SpecificationPending` até publicação de errata oficial pelo CONFAZ (T064).

---

## 4. Evidência de Execução dos Testes

Execução realizada via `infra/Test-Foundation.ps1`:

```
Execução de teste: Api.RAG.UnitTests.dll (net10.0)
Aprovado!  – Com falha: 0, Aprovado:   2, Total:   2

Execução de teste: Api.Agente.UnitTests.dll (net10.0)
Aprovado!  – Com falha: 0, Aprovado: 167, Total: 167

Execução de teste: AgenteDIMP.IntegrationTests.dll (net10.0)
Aprovado!  – Com falha: 0, Aprovado:  38, Total:  38

TOTAL: 207 testes aprovados (100% de sucesso).
```
