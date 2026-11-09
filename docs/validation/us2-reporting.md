# Evidência de Validação - User Story 2 (US2) - Relatórios e Achados

**Data de Validação:** 05/11/2026  
**Ambiente:** .NET 10 (Linux/Windows), PostgreSQL 16 (PgVector), Docker  
**Status da Jornada:** Concluída (100% dos testes aprovados)

---

## 1. Visão Geral

A User Story 2 (US2) estabelece a entrega de relatórios determinísticos e achados paginados com autoridade estrita do sistema operacional, garantindo:
1. Respostas HTTP sem ambiguidades para os endpoints `GET /api/v1/analises/{id}/relatorio` e `GET /api/v1/analises/{id}/achados`:
   - `200 OK`: Relatório completo ou página de achados com cursor estável.
   - `400 Bad Request`: `INVALID_CURSOR` (cursor inválido ou de outra execução) e `INVALID_PAGESIZE` (fora do intervalo 1–500).
   - `404 Not Found`: `EXECUTION_NOT_FOUND`.
   - `409 Conflict`: `ANALYSIS_IN_PROGRESS`.
   - `410 Gone`: `REPORT_EXPIRED` (ao atingir a janela de retenção de 30 dias).
   - `422 Unprocessable Entity`: `REPORT_UNAVAILABLE` (execuções finalizadas sem relatório).
2. As 5 categorias padronizadas de achados:
   - `erro_estrutural` (`StructuralError`)
   - `inconsistencia_negocio` (`BusinessInconsistency`)
   - `indicio_ia` (`AiIndication`)
   - `ausencia_evidencias` (`MissingEvidence`)
   - `falha_tecnica` (`TechnicalFailure`)
3. As 4 severidades padronizadas:
   - `bloqueante` (`Blocking`)
   - `erro` (`Error`)
   - `aviso` (`Warning`)
   - `informativo` (`Information`)
4. Política de Cobertura e Incompletude:
   - Igualdade estrita: `Applicable = Passed + Failed + NotExecuted + BlockedBySourceConflict`.
   - Categorias mutuamente exclusivas.
   - Estado terminal não equivale à conformidade: relatórios com violações, conflitos ou etapas pendentes exigem revisão humana (`requiresHumanReview = true`).
5. Autoridade do Envelope Operacional:
   - O modelo LLM nunca pode adulterar IDs, status, custos, totalizadores ou reduzir a revisão humana exigida pelo sistema (`ReportComposer`).
6. Bloqueio de Publicação sem Limpeza:
   - O relatório somente é publicado (`hasReport: true` e `EndedAt` gravado) após confirmação física da exclusão dos arquivos temporários (`ExecutionFinalizer`).

---

## 2. Testes de Integração e Unidade Executados

### 2.1 Testes de Contrato de Relatório e Achados (`ReportContractTests.cs`)
- `GetReport_Returns200_WhenReportExists`: APROVADO.
- `GetReport_Returns404_WhenExecutionDoesNotExist`: APROVADO.
- `GetReport_Returns409_WhenAnalysisStillInProgress`: APROVADO.
- `GetReport_Returns410_WhenReportIsExpired`: APROVADO.
- `GetReport_Returns422_WhenExecutionFailedWithoutReport`: APROVADO.
- `GetFindings_Returns200_WithPaginationCursorAndTotal`: APROVADO (valida navegação estável por cursor base64 e contagem total não truncada).
- `GetFindings_Returns400_WhenPageSizeIsOutOfBounds`: APROVADO (valida limites 1–500).
- `GetFindings_Returns400_WhenCursorIsCorruptOrFromDifferentExecution`: APROVADO (rejeição de cursor adulterado ou de outra execução).
- `GetFindings_Returns404_WhenExecutionDoesNotExist`: APROVADO.
- `GetFindings_Returns409_WhenAnalysisStillInProgress`: APROVADO.
- `GetFindings_Returns410_WhenReportIsExpired`: APROVADO.
- `GetFindings_Returns422_WhenExecutionFailedWithoutReport`: APROVADO.

### 2.2 Testes de Políticas de Relatório (`ReportPolicyTests.cs`)
- `Coverage_AggregatesStrictly_WithExclusiveCategories`: APROVADO.
- `Coverage_SameRuleWithMultipleOccurrences_AggregatesEachInstance`: APROVADO.
- `CategoriesAndSeverities_WireNamesMatchSpecification`: APROVADO.
- `Invariant_AbsenceOfEvidenceOrTechnicalFailure_DoesNotBecomeBusinessError`: APROVADO.
- `HumanReview_RequiredWhenViolationsExist_EvenIfStateIsCompleted`: APROVADO.
- `HumanReview_RequiredWhenSourceConflictExists`: APROVADO.
- `HumanReview_RequiredWhenMandatoryValidationNotExecuted`: APROVADO.
- `HumanReview_RequiredWhenAiIndicationOrUnprovenFindingPresent`: APROVADO.
- `HumanReview_NotRequiredWhenExecutionCleanAndAllEvaluatedRulesPass`: APROVADO.

### 2.3 Testes de Autoridade do Envelope (`OperationalEnvelopeTests.cs`)
- `ReportComposer_PreservesSystemEnvelope_IgnoringModelManipulations`: APROVADO.
- `ReportComposer_PreventsLoweringOfSystemHumanReview`: APROVADO.
- `ReportComposer_RaisesHumanReview_WhenModelIdentifiesNewLimitationOrIndication`: APROVADO.
- `ReportComposer_FiltersOutInvalidOrForeignReferencesFromModelExplanations`: APROVADO.

### 2.4 Testes de Resultados Parciais e Falhas Injetadas (`PartialResultsTests.cs`)
- `Report_WithPreConstructedPartialResults_ReflectsIncompletenessAndRequiresHumanReview`: APROVADO (validação com conflito SF-001 injetado e etapas não executadas sem depender da resolução fiscal oficial).
- `Report_PreservesDeterministicFindings_WhenAiAnalysisIsMissing`: APROVADO (degradação graciosa com preservação integral dos achados determinísticos).
- `InjectedCleanupFailure_PreventsReportPublication`: APROVADO (prova que falha de limpeza bloqueia a publicação e expiração do relatório).

---

## 3. Matriz de Rastreabilidade US2

| Componente | Artefato de Código | Responsabilidade | Status |
| --- | --- | --- | --- |
| Contrato e Schemas | `src/Api.Agente/Application/Contracts/AnalysisContracts.cs` | DTOs de Report, Finding, FindingsPage, Coverage, Location, Evidence, Problem | Conforme |
| Políticas de Negócio | `src/Api.Agente/Domain/Reports/ReportPolicies.cs` | Categorias, severidades, revisão humana e agregação de cobertura | Conforme |
| Compositor de Relatório | `src/Api.Agente/Application/Reports/ReportComposer.cs` | Montagem do envelope operacional, sanitização de referências de IA e fatos determinísticos | Conforme |
| Armazenamento e Consulta | `src/Api.Agente/Infrastructure/Reports/ReportStore.cs` | Persistência e consulta paginada com cursor seguro e proteção de expiração | Conforme |
| Construtor de Fluxo | `src/Api.Agente/Application/Reports/BuildExecutionReport.cs` | Integração da composição com persistência de entidades mascaradas | Conforme |
| Endpoint de Relatório | `src/Api.Agente/Endpoints/GetAnalysisReportEndpoint.cs` | `GET /api/v1/analises/{id}/relatorio` | Conforme |
| Endpoint de Achados | `src/Api.Agente/Endpoints/GetAnalysisFindingsEndpoint.cs` | `GET /api/v1/analises/{id}/achados` | Conforme |
| Orquestrador de Execução | `src/Api.Agente/Workers/AnalysisWorker.cs` | Integração da etapa de composição no worker determinístico | Conforme |
