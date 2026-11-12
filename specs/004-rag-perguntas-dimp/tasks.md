# Tarefas 004 — RAG para perguntas DIMP

## Fase 1 — Preparação

- [X] T001 Conferir hash, versão e origem do PDF S01 em `docs/rules/dimp-v10/manifest.json` e `docs/referencias/dimp-v10-manual-confaz.pdf`.
- [X] T002 Preparar PostgreSQL/pgvector local com `infra/compose.yaml` e aplicar migrações de `src/Api.RAG/Infrastructure/Persistence/Migrations/`; registrar o baseline do POST, do GET de status e do MCP em `specs/004-rag-perguntas-dimp/quickstart.md`.

## Fase 2 — Ingestão local (US1)

- [X] T003 [US1] Garantir registro consistente de revisão e fila, deduplicação concorrente e resolução do PDF pelo trabalho em `src/Api.RAG/Application/Ingestion/DocumentIngestionService.cs` e `src/Api.RAG/Infrastructure/Persistence/KnowledgeDbContext.cs`.
- [X] T004 [US1] Implementar claim, lease, renovação e fencing em `src/Api.RAG/Workers/DocumentIngestionWorker.cs` e registrar o consumidor em `src/Api.RAG/Infrastructure/Configuration/FoundationRegistration.cs`.
- [X] T005 [US1] Ligar o processamento aos checkpoints e à publicação do corpus completo em `src/Api.RAG/Application/Ingestion/DocumentIngestionService.cs`; filtrar resultados e acesso direto a trecho pelo estado de publicação em `src/Api.RAG/Infrastructure/Retrieval/ExactVectorSearch.cs` e `src/Api.RAG/Infrastructure/Mcp/ManualTools.cs`.
- [X] T006 [US1] Validar POST → GET concluído → MCP, reenvio concorrente e retomada sem republicar corpus parcial em `tests/AgenteDIMP.IntegrationTests/US3/IngestionContractTests.cs` e `tests/AgenteDIMP.IntegrationTests/US3/IngestionRecoveryTests.cs`.

**Aceite independente**: SC-001. Ingestão do PDF completa no ambiente local e citações recuperáveis por MCP.

## Fase 3 — Busca semântica (US2)

- [X] T007 [US2] Habilitar perfil explícito com chamadas externas e validação de chave, provedor, prazo e orçamento em `src/Api.RAG/Infrastructure/Development/BootstrapProfile.cs` e `src/Api.RAG/Infrastructure/Configuration/RagOptions.cs`.
- [X] T008 [US2] Isolar vetores determinísticos e semânticos e preservar o ledger em `src/Api.RAG/Infrastructure/AI/EmbeddingService.cs`. Reindexar S01 com o perfil semântico selecionado.
- [X] T009 [P] [US2] Separar perguntas de calibração e aceite em `tests/fixtures/rag/evaluation.json`, com páginas esperadas, consultas sem evidência e revisão incompatível.
- [X] T010 [US2] Executar a avaliação de recuperação em `tests/AgenteDIMP.IntegrationTests/US3/RetrievalEvaluationTests.cs` e registrar os resultados e parâmetros efetivos em `docs/validation/rag-evaluation.md`.

**Aceite independente**: SC-002. A busca semântica recupera as evidências esperadas e o perfil falso permanece isolado.

## Fase 4 — Perguntas no Swagger (US3)

- [X] T011 [US3] Disponibilizar o cliente MCP no perfil da demo com destino configurado, cancelamento e prazo em `src/Api.Agente/Infrastructure/Configuration/FoundationRegistration.cs` e `src/Api.Agente/Infrastructure/Rag/RagMcpClient.cs`.
- [X] T012 [US3] Encaminhar perguntas sem resposta no catálogo para `search_manual` e compor resposta citada com tratamento de ausência e falha em `src/Api.Agente/Application/Sofia/SofiaDimpQuestions.cs` e `src/Api.Agente/Endpoints/LocalDimpDemoEndpoint.cs`.
- [X] T013 [US3] Validar a chamada real ao MCP para uma pergunta aberta e verificar catálogo, ausência de evidência, indisponibilidade, arquivo e A2A em `specs/004-rag-perguntas-dimp/quickstart.md`.

**Aceite independente**: SC-003. A pergunta aberta tem citação do RAG e as chamadas existentes passam nas regressões.

## Fase 5 — Homologação e publicação (US4)

- [X] T014 [US4] Registrar a decisão de acesso para a hospedagem, validar credenciais distintas de consulta e administração e proteger o MCP e o REST de ingestão em `src/Api.RAG/Program.cs`, `src/Api.RAG/Endpoints/DocumentIngestionEndpoints.cs` e `src/Api.Agente/Infrastructure/Rag/RagMcpClient.cs`.
- [X] T015 [US4] Preparar contêiner e configuração da RAG, migrations em banco gerenciado, persistência do PDF e execução da ingestão em `src/Api.RAG/Dockerfile` e `specs/004-rag-perguntas-dimp/quickstart.md`.
- [X] T016 [US4] Validar acervo após reinício, autenticação, latência, falha do RAG, consulta citada e regressões no preview com `specs/004-rag-perguntas-dimp/quickstart.md`.
- [X] T017 [US4] Promover o deploy aprovado, repetir a verificação no domínio público e registrar as URLs, identificadores e reversão em `specs/004-rag-perguntas-dimp/quickstart.md`.

**Aceite independente**: SC-004. O Swagger publicado responde com trechos da revisão persistida e o procedimento de reversão foi verificado.

## Fase 6 — Encerramento

- [X] T018 Atualizar o estado da T091 conforme a evidência em `specs/001-validar-arquivo-dimp/tasks.md` e registrar a rastreabilidade de FR-001 a FR-007 em `docs/validation/traceability.md`.

## Ordem de execução

Fase 1 → US1 → US2 → US3 → US4 → encerramento. A preparação das perguntas de aceite (T009) pode ocorrer em paralelo à habilitação do perfil semântico. O primeiro incremento é a ingestão local, comprovada antes de criar infraestrutura hospedada.
