# Matriz de Rastreabilidade - Requisitos, Tarefas, Testes e Evidências

**Data de Conclusão:** 09/11/2026  
**Projeto:** AgenteDIMP - Validador de Arquivos DIMP com IA e RAG  
**Status Geral:** 100% dos Requisitos Funcionais (FR-001 a FR-032) e Critérios de Sucesso (SC-001 a SC-013) Cobertos

---

## 1. Matriz de Requisitos Funcionais (FR-001 a FR-032)

| Requisito | Descrição Resumida | Tarefas de Implementação | Testes Associados | Documento de Evidência |
|---|---|---|---|---|
| **FR-001** | Aceitar arquivo DIMP por requisição e gerar identificador único | T067, T068, T069 | `AnalysisSubmissionContractTests`, `AnalysisStatusEndpointTests` | `docs/validation/us1-deterministic.md` |
| **FR-002** | Verificar formato, encoding UTF-8, integridade e sanitização | T018, T027, T028 | `DimpParserTests`, `ContentSanitizerTests` | `docs/validation/us1-t021-t030.md` |
| **FR-003** | Identificar versão declarada (V10) no registro 0000 | T029, T030 | `LayoutResolverTests`, `DimpParserTests` | `docs/validation/us1-t021-t030.md` |
| **FR-004** | Leiaute V10 oficial como baseline normativo | T021, T022 | `RuleCatalogTests`, `DimpValidationEngineTests` | `docs/validation/us1-deterministic.md` |
| **FR-005** | Implementação de 100% das 29 regras determinísticas | T031–T060 | 29 suítes em `tests/Api.Agente.UnitTests/Validation/Records/` | `docs/validation/us1-deterministic.md` |
| **FR-006** | Catálogo identificando regras cobertas e não aplicáveis | T021, T022, T075 | `RuleCatalogTests`, `ReportPolicyTests` | `specs/001-validar-arquivo-dimp/rule-catalog.md` |
| **FR-007** | Achados com regra, descrição, localização e evidência | T075, T076 | `OperationalEnvelopeTests`, `ReportPolicyTests` | `docs/validation/us2-reporting.md` |
| **FR-008** | Cinco categorias de achados e severidades estritas | T075, T076 | `ReportPolicyTests`, `OperationalEnvelopeTests` | `docs/validation/us2-reporting.md` |
| **FR-009** | Consulta RAG restrita ao acervo aplicável à revisão | T085, T093, T095 | `RetrievalEvaluationTests`, `McpContractTests` | `docs/validation/rag-evaluation.md` |
| **FR-010** | Citações rastreáveis por página, seção e hash de chunk | T087, T089, T095 | `PdfDocumentExtractorTests`, `McpContractTests` | `docs/validation/us3-ai-rag.md` |
| **FR-011** | Análise por IA baseada estritamente em resumos mascarados | T100, T101, T103 | `ExternalPayloadPrivacyTests`, `ContextualAnalysisTests` | `docs/validation/us3-ai-rag.md` |
| **FR-012** | Entradas e manuais tratados estritamente como dados | T084, T100 | `ExternalPayloadPrivacyTests` | `docs/validation/us3-ai-rag.md` |
| **FR-013** | Validação semântica e schema estrito de resposta LLM | T083, T102 | `AnalyticResponseTests`, `ContextualAnalysisTests` | `docs/validation/us3-ai-rag.md` |
| **FR-014** | Relatório com identificador, status, severidade e revisão | T076, T078 | `ReportContractTests`, `OperationalEnvelopeTests` | `docs/validation/us2-reporting.md` |
| **FR-015** | Envelope com tempos, contagens, consumo e custos | T106, T109, T112 | `UsageAccountingTests`, `ExecutionUsageAggregatorTests` | `docs/validation/us4-audit-retention.md` |
| **FR-016** | Preservação de resultados determinísticos se IA falhar | T079, T103 | `ContextualAnalysisTests`, `PartialResultsTests` | `docs/validation/us2-reporting.md`, `us3-ai-rag.md` |
| **FR-017** | Não declarar conformidade com validação incompleta | T075, T076 | `ReportPolicyTests` | `docs/validation/us2-reporting.md` |
| **FR-018** | Apontamentos com referência normativa oficial do manual | T076, T094, T102 | `RetrievalEvaluationTests`, `ReportPolicyTests` | `docs/validation/rag-evaluation.md` |
| **FR-019** | Ledger de chamadas por execução e provedor pré-chamada | T106, T110 | `UsageAccountingTests`, `ExecutionAuditTests` | `docs/validation/us4-audit-retention.md` |
| **FR-020** | Consumo desconhecido permanece desconhecido (nulo != 0) | T106, T109, T112 | `UsageAccountingTests`, `ExecutionUsageAggregatorTests` | `docs/validation/us4-audit-retention.md` |
| **FR-021** | Não corrigir, assinar ou transmitir o arquivo | Escopo EX-001 | `OutputAndToolBoundaryTests` | `docs/validation/traceability.md` |
| **FR-022** | Exigência de revisão humana para indícios de IA | T074, T075, T102 | `OperationalEnvelopeTests`, `AnalyticResponseTests` | `docs/validation/us2-reporting.md` |
| **FR-023** | Severidades: bloqueante, erro, aviso, informativo | T075 | `ReportPolicyTests` | `docs/validation/us2-reporting.md` |
| **FR-024** | Sem gestão de usuários externa no marco EX-001 | Escopo EX-001 | `OutputAndToolBoundaryTests` | `docs/validation/traceability.md` |
| **FR-025** | Isolamento total de execuções sem contaminação cruzada | T077, T098, T108 | `CrossServiceUsageTests`, `MongoContextTests` | `docs/validation/us4-audit-retention.md` |
| **FR-026** | Revisão documental recuperável mesmo após atualizações | T077, T085, T095 | `McpContractTests`, `ReportContractTests` | `docs/validation/us3-ai-rag.md` |
| **FR-027** | Minimização e mascaramento pré-chamada LLM | T084, T100 | `ExternalPayloadPrivacyTests` | `docs/validation/us3-ai-rag.md` |
| **FR-028** | Arquivo original e mapa deletados após finalização | T018, T115 | `ExecutionFinalizerTests`, `RetentionBoundaryTests` | `docs/validation/us4-audit-retention.md` |
| **FR-029** | Relatório retido por 30 dias com expiração lógica | T077, T113 | `RetentionBoundaryTests`, `ReportContractTests` | `docs/validation/us4-audit-retention.md` |
| **FR-030** | Processamento assíncrono durável tolerante a desconexão | T070, T071 | `DurableRecoveryTests`, `AnalysisSubmissionContractTests` | `docs/validation/us1-deterministic.md` |
| **FR-031** | Consulta assíncrona de status via endpoint dedicado | T069 | `AnalysisStatusEndpointTests` | `docs/validation/us1-deterministic.md` |
| **FR-032** | Tombstone mantido por 90 dias após expiração (410 -> 404) | T113, T116 | `RetentionBoundaryTests`, `ExecutionAuditTests` | `docs/validation/us4-audit-retention.md` |

---

## 2. Matriz de Critérios de Sucesso (SC-001 a SC-013)

| Critério | Descrição | Status | Evidência Comprovada |
|---|---|---|---|
| **SC-001** | 100% dos exemplos de aceite recebem identificador e resultado determinístico compatível | APROVADO | `us1-deterministic.md`, `DimpValidationEngineTests` |
| **SC-002** | Zero declarações de conformidade em cenários de validação incompleta ou erro | APROVADO | `ReportPolicyTests` (`Verdict == ComInconsistencias`) |
| **SC-003** | 100% dos achados identificam regra, categoria, severidade e evidência | APROVADO | `us2-reporting.md`, `ReportContractTests` |
| **SC-004** | Zero citações inventadas aceitas nos cenários de ausência de evidência | APROVADO | `rag-evaluation.md`, `AnalyticResponseTests` |
| **SC-005** | Em indisponibilidade de IA ou RAG, analista recebe achados determinísticos | APROVADO | `ContextualAnalysisTests` (`AI_ANALYSIS_SKIPPED`) |
| **SC-006** | Consumo e retentativas correlacionados; nulo nunca vira zero | APROVADO | `UsageAccountingTests`, `us4-audit-retention.md` |
| **SC-007** | Zero mistura de dados ou achados entre execuções distintas | APROVADO | `CrossServiceUsageTests`, `MongoContextTests` |
| **SC-008** | Revisor consegue determinar o que foi verificado, o que falhou e o que exige revisão | APROVADO | `ReportPolicyTests`, `OperationalEnvelopeTests` |
| **SC-009** | 100% das 29 regras do manual constam no inventário com testes determinísticos | APROVADO | `rule-catalog.md`, 29 testes de registros |
| **SC-010** | Prompts enviados ao provedor de IA contêm zero dados fiscais não mascarados | APROVADO | `ExternalPayloadPrivacyTests`, `OutputAndToolBoundaryTests` |
| **SC-011** | Zero arquivos originais ou dados de reversão retidos após término | APROVADO | `OutputAndToolBoundaryTests`, `RetentionBoundaryTests` |
| **SC-012** | Relatório acessível antes de 30 dias e 410 Gone imediatamente ao expirar | APROVADO | `RetentionBoundaryTests`, `us4-audit-retention.md` |
| **SC-013** | Submissão aceita fornece identificador antes do término; polling funcional | APROVADO | `quickstart-results.md`, `AnalysisSubmissionContractTests` |

## Feature 004 — Perguntas DIMP com RAG

| Requisito | Tarefas | Verificação |
| --- | --- | --- |
| FR-001 — Proveniência e perfis | T001, T002 | [Manifesto S01](../rules/dimp-v10/manifest.json) e conferência do PDF no [roteiro](../../specs/004-rag-perguntas-dimp/quickstart.md). |
| FR-002 — Ingestão retomável e publicação atômica | T003–T006, T091 da Feature 001 | `IngestionContractTests`, `IngestionRecoveryTests` e transferência transacional de 492 vetores no Neon. |
| FR-003 — Isolamento e ledger | T007–T010 | `McpContractTests`, teste de perfil incompatível e conta de execução limitada no banco gerenciado. |
| FR-004 — Evidência e limitação | T009, T010, T013 | [Avaliação de recuperação](rag-evaluation.md), citações resolvidas por `get_manual_chunk` e aceite de 12 casos no MCP hospedado. |
| FR-005 — Pergunta sem arquivo e regressões | T011–T013 | `ManualQuestionIntegrationTests`, `A2AIntegrationTests` e consulta citada no Swagger. |
| FR-006 — Credenciais e operação hospedada | T014–T016 | MCP sem credencial `401`, ferramenta administrativa com credencial de consulta `403` e ingestão operada em instância separada. |
| FR-007 — Homologação e produção | T015–T017 | [Evidência da promoção e smoke tests](../../specs/004-rag-perguntas-dimp/quickstart.md#resultado-em-produção--2026-11-12), com identificação do deploy anterior para reversão. |
