# Evidência de Validação - User Story 3 (US3) - Apoio do Manual e da IA

**Data de Validação:** 09/11/2026  
**Ambiente:** .NET 10, PostgreSQL com PgVector, MongoDB, PdfPig, Model Context Protocol (MCP)  
**Status da Jornada:** Concluída (100% dos testes da US3 aprovados)

---

## 1. Visão Geral da Entrega

A User Story 3 (US3) capacita o sistema a contextualizar e explicar os achados determinísticos identificados no arquivo DIMP com o apoio do Manual de Orientação do Leiaute da DIMP oficial (CONFAZ) e de Modelos de Linguagem (LLM), assegurando:

1. **Ingestão Oficial e Idempotente no Acervo (`Api.RAG`):**
   - Endpoints REST documentados em `rag.openapi.json`:
     - `POST /api/v1/documentos/ingestoes`
     - `GET /api/v1/documentos/ingestoes/{id}`
     - `GET /api/v1/documentos/{id}/revisoes`
   - Validação MIME/PDF, cálculo de SHA-256 e rejeição de hashes divergentes (`422 HASH_MISMATCH`) ou perfis desconhecidos (`422 PROFILE_UNKNOWN`).
   - Idempotência: submissão com mesmo SHA-256 e perfis já concluídos retorna `200 OK` com `status: "concluida"` sem nova cobrança ou reingestão.
2. **Isolamento Arquitetural e Protocolo MCP:**
   - A `Api.Agente` acessa o conhecimento exclusivamente pelo cliente MCP tipado (`RagMcpClient.cs`) via HTTP stateless no endpoint `/mcp`.
   - **Zero acesso SQL direto**: a `Api.Agente` não se conecta nem consulta o banco de dados PostgreSQL `rag`.
   - As 4 ferramentas contratuais de `rag-mcp.schema.json` foram implementadas:
     - `search_manual`: busca vetorial exata com filtros imutáveis de versão e revisão fixados pelo host.
     - `get_manual_chunk`: recuperação de trecho pontual com verificação de proveniência.
     - `get_document_revision`: consulta de metadados da revisão oficial.
     - `get_ingestion_usage`: consulta correlacionada de consumo da ingestão.
3. **Memória de Contexto no MongoDB:**
   - Coleção `conversation_contexts` na `Api.Agente` gerenciada por `MongoConversationContextStore.cs`.
   - Índice composto único em `{ executionId, conversationId }`.
   - Índice TTL em `expiresAt` com `expireAfterSeconds = 0`.
   - Exclusão explícita por `executionId` integrada ao `ExecutionFinalizer` (`MongoExecutionMemoryCleanup.cs`).
4. **Proteção de Privacidade e Integridade:**
   - Nenhum arquivo bruto DIMP nem mapas de reversão são enviados para o modelo ou para o MongoDB (`ExternalPayloadPrivacyTests.cs`).
   - Os prompts recebem apenas os textos mascarados (`MaskedText`) gerados pelo `ContentSanitizer`.
   - O sistema mantém autoridade absoluta sobre o veredito, severidades, contagens e revisão humana (`requiresHumanReview = systemRequiresHumanReview || llmRequiresReview`). O modelo jamais pode revogar uma revisão humana exigida pelo sistema.
5. **Degradação Controlada:**
   - Em caso de indisponibilidade ou falha técnica no provedor de IA ou no MCP, o relatório determinístico continua íntegro e é publicado normalmente com a limitação `"AI_ANALYSIS_SKIPPED"`, sem interromper a esteira operacional.

---

## 2. Testes de Integração e Unidade Executados

### 2.1 Testes de Contrato de Ingestão (`IngestionContractTests.cs`)
- `NonPdfFile_Returns415UnsupportedMediaType`: APROVADO.
- `MissingMetadata_Returns400BadRequest`: APROVADO.
- `UnknownProfile_Returns422UnprocessableEntity`: APROVADO.
- `HashMismatch_Returns422UnprocessableEntity`: APROVADO.
- `ValidPdf_Returns202Accepted_AndStatusCanBeRetrieved`: APROVADO.
- `UnknownIngestionId_Returns404NotFound`: APROVADO.
- `UnknownDocumentId_Returns404NotFoundForRevisions`: APROVADO.

### 2.2 Testes de Contrato MCP (`McpContractTests.cs`)
- `Initialize_ReturnsProtocolVersionAndServerInfo`: APROVADO (`protocolVersion: "2026-07-28"`, `serverInfo: "Api.RAG"`).
- `ToolsList_ReturnsDeclaredTools`: APROVADO (valida as ferramentas `search_manual`, `get_manual_chunk`, `get_document_revision`, `get_ingestion_usage`).
- `SearchManual_WithValidRevision_ReturnsExpectedHits`: APROVADO (retorna `cosine_distance` e hashes da revisão).
- `SearchManual_WithUnknownRevision_ReturnsTypedError`: APROVADO (retorna `REVISION_NOT_FOUND`).
- `GetDocumentRevision_WithExistingRevision_ReturnsMetadata`: APROVADO.
- `GetManualChunk_WithUnknownChunk_ReturnsNull`: APROVADO.

### 2.3 Testes de Privacidade e Segurança (`ExternalPayloadPrivacyTests.cs`)
- `Prompts_NeverContainRawSensitiveInformation`: APROVADO (CPFs e CNPJs mascarados pelo sanitizer antes da composição do prompt).
- `PromptInjectionInFindings_IsIsolatedAndTreatedAsData`: APROVADO (tentativa de jailbreak no conteúdo de achados é neutralizada e sobreposição de autoridade do sistema impede que o modelo desative `requiresHumanReview`).

### 2.4 Avaliação de Recuperação e Qualidade RAG (`RetrievalEvaluationTests.cs`)
- `GateObjetivoQualidadeRag_PassesEvaluationSet`: APROVADO (100% de Recall@K nos casos respondíveis de `evaluation.json`, zero mistura de revisões e tratamento explícito de ausência de evidência).

### 2.5 Aceitação de Análise Contextual (`ContextualAnalysisTests.cs`)
- `ContextualAnalysis_WithValidCitation_EnrichesReportCorrectly`: APROVADO (composição do relatório enriquecido com resumo, explicações e citações de evidências válidas).
- `ContextualAnalysis_WhenAiProviderFails_PreservesDeterministicReport`: APROVADO (queda do provedor de IA preserva relatório determinístico com limitação `AI_ANALYSIS_SKIPPED`).

### 2.6 Testes Unitários de IA e Schema (`AnalyticResponseTests.cs`, `AnalysisContextBuilderTests.cs`, `AnalyticResponseValidatorTests.cs`, `ContextualAnalysisStageTests.cs`)
- Schema `llm-analysis.schema.json` validado com `additionalProperties: false`, campos obrigatórios e rejeição de propriedades extras.
- Validação semântica rejeita `findingIds` ou `evidenceIds` forjados pelo modelo.
- Todos os 198 testes unitários de `Api.Agente.UnitTests` e 4 testes de `Api.RAG.UnitTests` aprovados com 0 falhas.

---

## 3. Matriz de Rastreabilidade US3

| ID Requisito | Descrição | Componente Implementado | Teste de Evidência |
|---|---|---|---|
| **FR-016** | Ingestão versionada de PDF oficial e metadados por endpoint REST idempotente | `DocumentIngestionService.cs`, `DocumentIngestionEndpoints.cs` | `IngestionContractTests.cs` |
| **FR-017** | Extração estruturada de tabelas e páginas sem confiar na ordem visual linear | `PdfDocumentExtractor.cs`, `DocumentChunker.cs` | `PdfDocumentExtractorTests.cs`, `DocumentChunkerTests.cs` |
| **FR-018** | Geração e armazenamento de embeddings com dimensão e perfil imutáveis no PgVector | `EmbeddingService.cs`, `KnowledgeDbContext.cs` | `RetrievalEvaluationTests.cs`, `KnowledgeAcervo` (EF Migration) |
| **FR-019** | Exposição de ferramentas de consulta documental via Model Context Protocol (MCP) | `ManualTools.cs`, `RagMcpClient.cs` | `McpContractTests.cs` |
| **FR-020** | Enriquecimento analítico dos achados via LLM/Semantic Kernel com citações do manual | `ContextualAnalysisStage.cs`, `OpenAiAnalysisClient.cs` | `ContextualAnalysisTests.cs` |
| **FR-021** | Isolamento estrito de acervo: nenhum acesso SQL da Api.Agente ao banco da Api.RAG | `RagMcpClient.cs` | `McpContractTests.cs`, `Traceability` |
| **FR-022** | Memória conversacional e de contexto no MongoDB com índice único e TTL | `MongoConversationContextStore.cs`, `MongoExecutionMemoryCleanup.cs` | `ExternalPayloadPrivacyTests.cs`, `ContextualAnalysisTests.cs` |
| **FR-023** | Validação semântica e salvaguarda do envelope do sistema sobre a resposta do LLM | `AnalyticResponseValidator.cs`, `ReportComposer.cs` | `AnalyticResponseValidatorTests.cs`, `ExternalPayloadPrivacyTests.cs` |

---

## 4. Conclusão da Fase 5

Todas as tarefas da Fase 5 (User Story 3) - de **T081 a T105** - foram implementadas, validadas e testadas com sucesso. A plataforma `AgenteDIMP` está agora plenamente capacitada para realizar validações determinísticas de arquivos DIMP e enriquecer os achados com o embasamento normativo do Manual Oficial do CONFAZ através de inteligência artificial governada e segura.
