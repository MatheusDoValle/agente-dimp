# Tasks: Validar arquivo DIMP com análise rastreável

**Input**: Artefatos de specs/001-validar-arquivo-dimp/.
**Prerequisites**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md),
[data-model.md](data-model.md), [contratos](contracts/), [rule-catalog.md](rule-catalog.md)
e [quickstart.md](quickstart.md).
**Constituição**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md), versão 1.0.1.
**Gerado em**: 2026-10-29.
**Status**: Fase 1 concluída (T001–T006); demais 117 tarefas pendentes. Ver [evidência](../../docs/validation/phase1-setup.md).
**Total**: 123 tarefas em 7 fases.

**Tests**: incluídos porque spec.md exige testes por regra, cenários de aceite e
SC-001 a SC-013; não são testes inventados apenas para reproduzir a implementação.
Escrever os testes de cada jornada antes do código correspondente, observar a falha
esperada e registrar evidência real após implementar.

**Organization**: jornadas US1(P1), US2(P1), US3(P2) e US4(P3), preservando suas prioridades.
MongoDB Atlas é a memória escolhida; testes usam MongoDB local.
Vetores e acervo pertencem à Api.RAG em PostgreSQL com pgvector.
Execuções, fila, relatórios e consumo permanecem em PostgreSQL da Api.Agente.

## Format: ID, P e Story

Cada linha tem checkbox de execução, ID sequencial, [P] quando aplicável, rótulo da jornada
somente em sua fase, descrição, caminho de arquivo e pré-requisitos explícitos.
[P] permite trabalho paralelo SOMENTE depois de concluídas suas dependências;
não autoriza começar uma tarefa bloqueada nem alterar arquivos compartilhados sem coordenação.

## Path Conventions

Todos os caminhos de implementação são relativos à raiz C:/Infis/CursoIa/AgenteDIMP.
Arquivos de código, testes, configuração e documentos citados podem ainda não existir:
criá-los é trabalho das tarefas, não evidência de que foram implementados.
Não criar projetos de negócio adicionais, Redis, frontend, autorização ou deploy/A2A
neste marco. Alterações de arquitetura exigem os ADRs aplicáveis.

## Regras de execução e aceite

- O escopo final inclui TODAS as regras estruturais e de negócio de S01, não apenas
  um subconjunto. Um primeiro incremento pode demonstrar progresso sem ser aceito
  como a entrega completa.
- SF-001/002/003 são impedimentos documentais, não erros de arquivos de clientes.
  Se a pesquisa oficial não resolver uma divergência, manter a tarefa correspondente
  aberta e continuar tarefas independentes. Não solicitar/produzir uma interpretação
  fiscal do LLM para desbloquear o aceite.
- T064, T071 e os gates finais não são concluídos sem evidência oficial de resolução
  das regras afetadas. Nenhum teste pode escolher o resultado desejado por conveniência.
- O tamanho máximo de produto continua indefinido por decisão do responsável.
  Medir exemplos menores e limites do host; não substituir essa decisão por um número arbitrário.
- Acesso externo, credenciais reais e cobrança de provedor não são concedidos pela
  geração desta lista. Testes LiveProvider são opt-in com contexto/permissões adequados.
- Referências a campos/valores entre aspas preservam restrições do modelo de dados;
  contratos JSON/OpenAPI são a fonte dos demais tipos, required e nulabilidade.
- Nenhuma tarefa autoriza commit, push ou deploy automático.

## Phase 1: Setup (Shared Infrastructure)

**Goal**: Preparar somente os dois projetos principais e as ferramentas de desenvolvimento local.

**Independent Test**: Restore/build e descoberta dos projetos de teste; nenhum recurso remoto é provisionado automaticamente.

- [x] T001 Criar AgenteDIMP.slnx, src/Api.Agente/Api.Agente.csproj e src/Api.RAG/Api.RAG.csproj com net10.0 e módulos Endpoints/Application/Domain/Infrastructure, mantendo Workers dentro da Api.Agente. (Sem dependências.)
- [x] T002 Fixar SDK disponível em global.json e versões estáveis compatíveis em Directory.Packages.props, incluindo MongoDB.Driver, Npgsql/EF Core, pgvector, SQLite, MCP, Semantic Kernel, OpenAI e PdfPig; gerar locks após restore/build e registrar incompatibilidades em docs/validation/dependencies.md. (Depende de: T001.)
- [x] T003 [P] Criar .gitignore e .dockerignore cobrindo segredos, bin/obj, temporários DIMP, índices SQLite e mapas de mascaramento; preservar as skills e a cópia oficial do manual. (Depende de: T001.)
- [x] T004 [P] Definir PostgreSQL/pgvector e MongoDB local isolado em infra/compose.yaml, com bancos/papéis agente e rag distintos e imagens fixadas por versão/digest; documentar Atlas como destino gerenciado em infra/README.md, sem criar cluster ou liberar acesso externo. (Depende de: T001.)
- [x] T005 Criar tests/Api.Agente.UnitTests/Api.Agente.UnitTests.csproj, tests/Api.RAG.UnitTests/Api.RAG.UnitTests.csproj e tests/AgenteDIMP.IntegrationTests/AgenteDIMP.IntegrationTests.csproj com xUnit, WebApplicationFactory e containers de teste; separar Category=LiveProvider. (Depende de: T002.)
- [x] T006 Definir perfis LocalDeterministic e LocalReal em src/Api.Agente/appsettings.LocalDeterministic.json e src/Api.RAG/appsettings.LocalDeterministic.json, com provedores fake offline; documentar nomes externos de configuração e ausência de segredos em docs/configuration.md. (Depende de: T002.)

**Checkpoint**: Solução, ferramentas e perfis offline preparados; não há aceitação de funcionalidade neste ponto.

## Phase 2: Foundational (Blocking Prerequisites)

**Goal**: Estabelecer contratos, persistência, privacidade e ciclo de vida necessários para todas as jornadas.

**Independent Test**: Modelos/contratos compilam; testes de fundação provam isolamento de bancos, mascaramento e encerramento seguro.

- [ ] T007 Implementar contratos de domínio e DTOs operacionais versionados em src/Api.Agente/Application/Contracts/AnalysisContracts.cs e DTOs administrativos em src/Api.RAG/Application/Contracts/DocumentContracts.cs, a partir de specs/001-validar-arquivo-dimp/contracts/agente.openapi.json e rag.openapi.json; preservar required, nulabilidade, enums e campos gerados pelo sistema. (Depende de: T005, T006.)
- [ ] T008 Implementar relógio injetável, UUIDs gerados pelo sistema e correlação em src/Api.Agente/Application/ExecutionContext.cs e src/Api.RAG/Application/OperationContext.cs; persistir datas em UTC e evitar dados de configuração secretos nos snapshots; registrar operatorId/contextId do host e identityAssurance=unverified_local_configuration em cada execução, rejeitando identificação vazia sem criar login/autorização. (Depende de: T007.)
- [ ] T009 Criar Execution em src/Api.Agente/Domain/Execution.cs com "endedAt anulável", status "recebida, em_processamento, concluida, concluida_parcialmente, falhou, cancelada", stage "recebimento, identificacao, validacao, recuperacao, analise, composicao, limpeza" e verdict "sem_inconsistencias_nas_regras_avaliadas, com_inconsistencias, inconclusivo, nao_suportado"; não equiparar status a conformidade. (Depende de: T008.)
- [ ] T010 Criar RuleEvaluation, Finding e Location em src/Api.Agente/Domain/Validation/RuleEvaluation.cs, src/Api.Agente/Domain/Validation/Finding.cs e src/Api.Agente/Domain/Validation/DocumentLocation.cs; preservar result "passed, failed, not_applicable, not_executed, blocked_by_source_conflict", categoria "erro_estrutural, inconsistencia_negocio, indicio_ia, ausencia_evidencias, falha_tecnica", severidade "bloqueante, erro, aviso, informativo", "ruleId anulável para indício" e "linha baseada em 1"; não persistir valor original; definir coverage por instância exclusiva (ruleId, ocorrência) e validar applicable = passed + failed + notExecuted + blockedBySourceConflict conforme data-model.md. (Depende de: T007.)
- [ ] T011 Criar Report e EvidenceSnapshot em src/Api.Agente/Domain/Reports/Report.cs e src/Api.Agente/Domain/Reports/EvidenceSnapshot.cs com "executionId único", "expiresAt = endedAt + 30 dias; não renovar ao consultar." e distinção de evidências de manual/arquivo mascarado; IDs e metadados operacionais pertencem ao sistema. (Depende de: T009, T010.)
- [ ] T012 Configurar DbContext, credenciais separadas e migrações explícitas em src/Api.Agente/Infrastructure/Persistence/AgentDbContext.cs e src/Api.RAG/Infrastructure/Persistence/KnowledgeDbContext.cs; provar que o papel agente não consulta tabelas rag em tests/AgenteDIMP.IntegrationTests/Foundation/DataOwnershipTests.cs. (Depende de: T004, T011.)
- [ ] T013 Definir ProviderCallAttempt e repositórios de ledger em src/Api.Agente/Infrastructure/Usage/ProviderCallAttemptStore.cs e src/Api.RAG/Infrastructure/Usage/ProviderCallAttemptStore.cs; preservar "executionId anulável só para ingestão de acervo", "tokensInput, tokensOutput, cachedTokens e embeddingTokens são anuláveis." e "usageState: reported, partial, unknown."; bloquear nova chamada se o registro pré-chamada falhar. (Depende de: T012.)
- [ ] T014 Implementar opções validadas no startup em src/Api.Agente/Infrastructure/Configuration/ProcessingOptions.cs e src/Api.RAG/Infrastructure/Configuration/RagOptions.cs para modelos, deadlines e budgets finitos; até 3 tentativas transitórias totais e até 1 reparo semântico dentro do orçamento global; tamanho máximo de produto continua indefinido, sem declarar suporte ilimitado. (Depende de: T006, T008.)
- [ ] T015 Escrever testes inicialmente falhos em tests/Api.Agente.UnitTests/Privacy/ContentSanitizerTests.cs que CPF/CNPJ/nomes conhecidos não saem por descrições, trechos ou falhas; manter substituições consistentes por execução e não rotular mascaramento como anonimização integral. (Depende de: T010, T014.)
- [ ] T016 Implementar mascaramento consistente por execução e filtro de mensagens/evidências em src/Api.Agente/Application/Privacy/ContentSanitizer.cs; manter mapa reversível apenas na área temporária e aplicar a resumos, descrições interpoladas, logs e futuras chamadas externas. (Depende de: T015.)
- [ ] T017 Implementar área privada temporária e metadados TemporaryArtifact em src/Api.Agente/Infrastructure/TemporaryStorage/TemporaryArtifactStore.cs; tipos "original, parser_index, mask_map, upload_partial", caminhos gerados pelo sistema, sem histórico/logs/backups, incluindo exclusão de sidecars SQLite. (Depende de: T012, T016.)
- [ ] T018 Implementar coordenador de limpeza e transição terminal em src/Api.Agente/Application/Lifecycle/ExecutionFinalizer.cs; respeitar "Só gravar endedAt depois de confirmar exclusão dos artefatos transitórios." e "Falha de limpeza mantém etapa limpeza com alerta operacional e impede falso encerramento."; contrato inclui limpeza posterior do contexto MongoDB. (Depende de: T009, T017.)
- [ ] T019 Testar contratos de fundação em tests/AgenteDIMP.IntegrationTests/Foundation/LifecycleAndIsolationTests.cs: rejeitar payload bruto persistido, separar execuções, manter etapa limpeza diante de erro e não anunciar exclusão não confirmada. (Depende de: T015, T018.)
- [ ] T020 Implementar Problem responses, logs sanitizados e traces sem corpos/prompts/chaves em src/Api.Agente/Infrastructure/Observability/RedactedTelemetry.cs e src/Api.RAG/Infrastructure/Observability/RedactedTelemetry.cs; usar correlationId em falhas e manter EX-001 sem adicionar framework de autorização. (Depende de: T013, T019.)

**Checkpoint**: Fundação disponível; trabalho de jornadas pode começar conforme o grafo, sem remover os gates fiscais.

## Phase 3: User Story 1 - Receber e validar um arquivo DIMP (Priority: P1)

**Goal**: Receber um arquivo, retornar identificação e executar todas as regras determinísticas aplicáveis, com rastreabilidade e consulta básica.

**Independent Test**: Arquivos sintéticos menores validam recepção 202, desconexão, estados, regras esperadas e diagnóstico determinístico sem depender de RAG/LLM. Cobertura completa depende da resolução oficial de SF-001/002/003.

Testes e inventário aparecem antes da implementação correspondente; tarefas de modelos/serviços/endpoints seguem suas dependências.

- [ ] T021 [US1] Definir o schema e o manifesto do inventário em docs/rules/dimp-v10/catalog.schema.json e docs/rules/dimp-v10/manifest.json, com ID interno DIMP10, versão/hash S01, localizador, aplicabilidade, interpretação da fonte, validatorId e vínculos aos testes; distinguir cobertura de implementação de resultado por execução. (Depende de: T020.)
- [ ] T022 [US1] Inventariar todas as páginas e registros de S01 em docs/rules/dimp-v10/source-matrix.csv e docs/rules/dimp-v10/catalog.json, incluindo tabelas, observações, regras condicionais, relações, totalizadores e páginas sem regra executável justificadas; os 29 registros não equivalem a 29 regras. (Depende de: T021.)
- [ ] T023 [P] [US1] Investigar SF-001 em docs/rules/dimp-v10/source-conflicts/SF-001.md e procurar resolução oficial para COD_FIN=9; registrar fonte/revisão/hash/localizador e impacto, ou manter impedimento explícito sem marcar esta tarefa concluída enquanto faltar resolução. (Depende de: T022.)
- [ ] T024 [P] [US1] Investigar SF-002 em docs/rules/dimp-v10/source-conflicts/SF-002.md e procurar resolução oficial para o tamanho de COD_MUN_DEST no registro 1120; registrar evidência e impacto, ou manter impedimento explícito sem escolher 7/8 por conveniência. (Depende de: T022.)
- [ ] T025 [P] [US1] Investigar SF-003 em docs/rules/dimp-v10/source-conflicts/SF-003.md e procurar resolução oficial de IND_TP_PIX/natureza 12 no registro 1115; registrar evidência e impacto, ou manter impedimento explícito sem extrapolar outra regra. (Depende de: T022.)
- [ ] T026 [US1] Preparar fixtures sintéticas menores e resultados independentes em tests/fixtures/dimp-v10/ e tests/fixtures/dimp-v10/expected/manifest.json; cobrir validade, erros, condições, versões não suportadas e conflitos, registrar tamanho real sem impor teto de produto. (Depende de: T022.)
- [ ] T027 [P] [US1] Escrever testes de contrato de upload/status em tests/AgenteDIMP.IntegrationTests/US1/AnalysisSubmissionContractTests.cs cobrindo 202, recepção incompleta, vazio, host limit, 503 sem aceitação durável e continuidade após desconexão. (Depende de: T026.)
- [ ] T028 [P] [US1] Escrever testes físicos/hierárquicos em tests/Api.Agente.UnitTests/Validation/DimpParserTests.cs com bytes, campos vazios, terminadores, offsets, hierarquia e versão; usar expectativas extraídas de S01, sem inferir codificação apenas porque Latin-1 decodificou. (Depende de: T026.)
- [ ] T029 [P] [US1] Escrever testes parametrizados por regra em tests/Api.Agente.UnitTests/Validation/RuleCatalogTests.cs e tests/AgenteDIMP.IntegrationTests/US1/RuleCoverageTests.cs, com positivo/negativo/condicional e matriz fonte→regra→validador→teste; testes de regras impedidas exigem evidência oficial. (Depende de: T026.)
- [ ] T030 [US1] Criar registro carregável e verificador do catálogo em src/Api.Agente/Domain/Validation/RuleCatalog.cs, com revisão/hash imutáveis por execução e resultados "passed, failed, not_applicable, not_executed, blocked_by_source_conflict"; não transformar regra não implementada em não aplicável. (Depende de: T029.)
- [ ] T031 [US1] Implementar leitura sequencial de bytes e tokenização em src/Api.Agente/Infrastructure/Dimp/DimpStreamReader.cs e src/Api.Agente/Domain/Validation/DimpParser.cs preservando vazios, linha baseada em 1 e offset quando conhecido, sem normalização que esconda violações. (Depende de: T028, T030.)
- [ ] T032 [US1] Implementar identificação V10 e contexto aplicável em src/Api.Agente/Domain/Validation/LayoutResolver.cs, com S01 fixada por hash, período e versão avaliados conforme catálogo; tipo/versão desconhecidos ou não suportados não ativam fallback silencioso. (Depende de: T031.)
- [ ] T033 [US1] Implementar índices e agregações temporários em src/Api.Agente/Infrastructure/Dimp/TemporaryValidationIndex.cs com SQLite por execução, comparação decimal, referências estáveis e limpeza dos sidecars; não carregar todo o arquivo em RAM. (Depende de: T017, T031.)
- [ ] T034 [US1] Implementar primitivas de campo, formatos, ocorrências e estrutura em src/Api.Agente/Domain/Validation/Common/FieldValueRules.cs e cenários correspondentes em tests/Api.Agente.UnitTests/Validation/CommonRulesTests.cs, aplicando apenas regras rastreadas no catálogo, com aritmética decimal e cultura explícita. (Depende de: T029, T031, T032, T033.)
- [ ] T035 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0000ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0000Validator.cs para todas as regras não conflitantes do registro 0000, com referências do catálogo e dados de contexto necessários. Preservar SF-001 como impedimento até resolução oficial. (Depende de: T034.)
- [ ] T036 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0001ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0001Validator.cs para todas as regras não conflitantes do registro 0001, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T037 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0002ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0002Validator.cs para todas as regras não conflitantes do registro 0002, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T038 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0005ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0005Validator.cs para todas as regras não conflitantes do registro 0005, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T039 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0006ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0006Validator.cs para todas as regras não conflitantes do registro 0006, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T040 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0100ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0100Validator.cs para todas as regras não conflitantes do registro 0100, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T041 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0105ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0105Validator.cs para todas as regras não conflitantes do registro 0105, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T042 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0109ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0109Validator.cs para todas as regras não conflitantes do registro 0109, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T043 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0200ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0200Validator.cs para todas as regras não conflitantes do registro 0200, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T044 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0201ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0201Validator.cs para todas as regras não conflitantes do registro 0201, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T045 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0209ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0209Validator.cs para todas as regras não conflitantes do registro 0209, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T046 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0300ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0300Validator.cs para todas as regras não conflitantes do registro 0300, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T047 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0600ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0600Validator.cs para todas as regras não conflitantes do registro 0600, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T048 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0700ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0700Validator.cs para todas as regras não conflitantes do registro 0700, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T049 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record0990ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record0990Validator.cs para todas as regras não conflitantes do registro 0990, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T050 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1001ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1001Validator.cs para todas as regras não conflitantes do registro 1001, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T051 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1100ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1100Validator.cs para todas as regras não conflitantes do registro 1100, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T052 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1110ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1110Validator.cs para todas as regras não conflitantes do registro 1110, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T053 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1115ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1115Validator.cs para todas as regras não conflitantes do registro 1115, com referências do catálogo e dados de contexto necessários. Preservar SF-003 como impedimento até resolução oficial. (Depende de: T034.)
- [ ] T054 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1120ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1120Validator.cs para todas as regras não conflitantes do registro 1120, com referências do catálogo e dados de contexto necessários. Preservar SF-002 como impedimento até resolução oficial. (Depende de: T034.)
- [ ] T055 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1200ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1200Validator.cs para todas as regras não conflitantes do registro 1200, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T056 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1220ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1220Validator.cs para todas as regras não conflitantes do registro 1220, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T057 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1500ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1500Validator.cs para todas as regras não conflitantes do registro 1500, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T058 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1600ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1600Validator.cs para todas as regras não conflitantes do registro 1600, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T059 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record1990ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record1990Validator.cs para todas as regras não conflitantes do registro 1990, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T060 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record9001ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record9001Validator.cs para todas as regras não conflitantes do registro 9001, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T061 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record9900ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record9900Validator.cs para todas as regras não conflitantes do registro 9900, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T062 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record9990ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record9990Validator.cs para todas as regras não conflitantes do registro 9990, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T063 [P] [US1] Escrever primeiro testes positivos/negativos/condicionais em tests/Api.Agente.UnitTests/Validation/Records/Record9999ValidatorTests.cs e implementar src/Api.Agente/Domain/Validation/Records/Record9999Validator.cs para todas as regras não conflitantes do registro 9999, com referências do catálogo e dados de contexto necessários. (Depende de: T034.)
- [ ] T064 [US1] Aplicar somente resoluções oficiais de SF-001/002/003 aos validadores src/Api.Agente/Domain/Validation/Records/Record0000Validator.cs, Record1120Validator.cs e Record1115Validator.cs e aos testes respectivos; atualizar docs/rules/dimp-v10/catalog.json com a proveniência da interpretação sem reescrever S01. (Depende de: T023, T024, T025, T035, T053, T054.)
- [ ] T065 [US1] Registrar os 29 validadores e as relações de pai/filho, obrigatoriedade e contadores em src/Api.Agente/Domain/Validation/DimpValidationEngine.cs; preservar a distinção entre hierarquia e exceções explícitas de totalização do manual, registrando todo resultado de regra e impedimento. (Depende de: T035, T036, T037, T038, T039, T040, T041, T042, T043, T044, T045, T046, T047, T048, T049, T050, T051, T052, T053, T054, T055, T056, T057, T058, T059, T060, T061, T062, T063.)
- [ ] T066 [US1] Implementar ProcessingJob/StageCheckpoint e repositório de reserva em src/Api.Agente/Infrastructure/Jobs/PostgresJobStore.cs com "Chave única por executionId.", estados internos "pending, leased, completing, completed", lease, heartbeat, fencing token e transação curta com SKIP LOCKED; não manter transação durante dependências externas. (Depende de: T012, T018, T027.)
- [ ] T067 [US1] Implementar recepção multipart streaming em src/Api.Agente/Endpoints/SubmitAnalysisEndpoint.cs e src/Api.Agente/Application/SubmitAnalysis.cs, com arquivo íntegro/hash e execução+job confirmados antes do 202; limpar parcial/órfão em falha e gerar statusUrl/reportUrl relativos sem aguardar análise. (Depende de: T027, T066.)
- [ ] T068 [US1] Implementar worker e checkpoints em src/Api.Agente/Workers/AnalysisWorker.cs, compondo parser/validadores e persistindo somente resultados mascarados; a fase inicial usa porta de análise contextual desabilitada explicitamente, marca parte de IA não realizada e nunca simula fluxo completo. (Depende de: T065, T066, T067.)
- [ ] T069 [US1] Implementar status, resultado determinístico mínimo e consulta básica em src/Api.Agente/Endpoints/GetAnalysisStatusEndpoint.cs, src/Api.Agente/Endpoints/GetAnalysisReportEndpoint.cs e src/Api.Agente/Application/Reports/DeterministicReportBuilder.cs respeitando DTOs, versão do contrato e metadados exclusivamente do sistema. (Depende de: T011, T068.)
- [ ] T070 [US1] Testar leases concorrentes, trabalho aceito antes/depois do crash, arquivo ausente e worker com posse vencida em tests/AgenteDIMP.IntegrationTests/US1/DurableJobRecoveryTests.cs; confirmar idempotência e que desconectar após 202 não cancela o trabalho. (Depende de: T068, T069.)
- [ ] T071 [US1] Executar suíte determinística US1 e registrar matriz de cobertura em docs/validation/us1-deterministic.md e docs/rules/dimp-v10/coverage.json, exigindo 100% das regras aplicáveis com teste e zero impedimentos para declarar cobertura completa; produzir evidência parcial honesta enquanto SF-001/002/003 bloquearem regras. (Depende de: T064, T069, T070.)

**Checkpoint**: Submissão e diagnóstico determinístico demonstráveis sem IA. Aceite de cobertura completa exige T064/T071 e fontes resolvidas.

## Phase 4: User Story 2 - Receber um resultado completo ou parcial sem ambiguidade (Priority: P1)

**Goal**: Compor relatório e achados com categorias, severidades, limitações e conformidade sem confundir falha técnica, erro do documento ou indício.

**Independent Test**: Injetar resultados conhecidos de validadores e etapas que falham/ficam pendentes; conferir categorias, revisão humana, envelope e relatório parcial sem depender de OpenAI.

Testes e inventário aparecem antes da implementação correspondente; tarefas de modelos/serviços/endpoints seguem suas dependências.

- [ ] T072 [P] [US2] Escrever testes dos contratos de relatório/achados em tests/AgenteDIMP.IntegrationTests/US2/ReportContractTests.cs, cobrindo 200/400/404/409/410/422, ordenação estável, cursor vinculado à execução e pageSize com mínimo 1, máximo 500 e default 100. (Depende de: T020.)
- [ ] T073 [P] [US2] Escrever testes de conclusão, severidade e revisão humana em tests/Api.Agente.UnitTests/Reports/ReportPolicyTests.cs cobrindo as cinco categorias, validação obrigatória não concluída, conflito de fonte e indício sem comprovação; incluir a mesma regra passando/falhando em ocorrências distintas, categorias de coverage exclusivas e sua igualdade de agregação. (Depende de: T020.)
- [ ] T074 [P] [US2] Escrever testes de autoridade do envelope em tests/Api.Agente.UnitTests/Reports/OperationalEnvelopeTests.cs para rejeitar ID/status/custo/contagem inventados pelo modelo e impedir redução da revisão humana exigida pelo sistema. (Depende de: T020.)
- [ ] T075 [US2] Implementar políticas em src/Api.Agente/Domain/Reports/ReportPolicies.cs preservando categorias "erro_estrutural, inconsistencia_negocio, indicio_ia, ausencia_evidencias, falha_tecnica" e severidades "bloqueante, erro, aviso, informativo"; estado terminal não equivale à conformidade e indício permanece sujeito a revisão. (Depende de: T073.)
- [ ] T076 [US2] Implementar composição do relatório em src/Api.Agente/Application/Reports/ReportComposer.cs com catálogo/achados reais, cobertura, referências, limitações, etapas não realizadas e revisão humana; agregar o conteúdo analítico opcional sem permitir que altere fatos determinísticos ou metadados operacionais. (Depende de: T074, T075.)
- [ ] T077 [US2] Implementar persistência/consulta de Report e EvidenceSnapshot em src/Api.Agente/Infrastructure/Reports/ReportStore.cs preservando "executionId único", evidências mascaradas e "expiresAt = endedAt + 30 dias; não renovar ao consultar."; manter revisão documental recuperável enquanto houver relatório referenciado. (Depende de: T076.)
- [ ] T078 [US2] Completar consultas de relatório e achados em src/Api.Agente/Endpoints/GetAnalysisReportEndpoint.cs e src/Api.Agente/Endpoints/GetAnalysisFindingsEndpoint.cs com respostas distinguíveis de espera/falha/expiração/inexistência; devolver todos os achados por paginação estável sem truncar o total. (Depende de: T069, T072, T077.)
- [ ] T079 [US2] Integrar composição completa ao fluxo em src/Api.Agente/Application/Reports/BuildExecutionReport.cs, preservando achados determinísticos quando a análise contextual faltar; publicar relatório apenas após o coordenador de finalização confirmar limpeza. (Depende de: T018, T068, T076, T078.)
- [ ] T080 [US2] Executar teste independente de US2 com resultados pré-construídos e falhas injetadas em tests/AgenteDIMP.IntegrationTests/US2/PartialResultsTests.cs; registrar evidência em docs/validation/us2-reporting.md, sem depender da resolução fiscal dos conflitos para testar a política de incompletude. (Depende de: T079.)

**Checkpoint**: Relatórios, categorias e incompletude verificáveis com resultados construídos, preservando a autoridade do sistema.

## Phase 5: User Story 3 - Entender os achados com apoio do manual e da IA (Priority: P2)

**Goal**: Ingerir o manual versionado, recuperar evidências por MCP e explicar achados por IA com minimização, memória MongoDB e saída validada.

**Independent Test**: Com resultados determinísticos conhecidos e manual de teste, conferir revisão/citações, ausência de evidências, falhas RAG/IA, conteúdo malicioso e mascaramento. Double de provedor permite teste independente; integração real é opt-in.

Testes e inventário aparecem antes da implementação correspondente; tarefas de modelos/serviços/endpoints seguem suas dependências.

- [ ] T081 [P] [US3] Escrever testes dos contratos REST de ingestão/revisões em tests/AgenteDIMP.IntegrationTests/US3/IngestionContractTests.cs, cobrindo hash divergente, perfil ausente, PDF inválido, 202 novo e 200 para revisão/perfis já concluídos sem nova cobrança. (Depende de: T020.)
- [ ] T082 [P] [US3] Escrever testes MCP entre os serviços em tests/AgenteDIMP.IntegrationTests/US3/McpContractTests.cs para search_manual/get_manual_chunk/get_document_revision, validação contra rag-mcp.schema.json, filtros fixos, perfil incompatível, erros tipados, proveniência e nenhuma consulta SQL do agente ao banco rag. (Depende de: T020.)
- [ ] T083 [P] [US3] Escrever testes de schema/semântica em tests/Api.Agente.UnitTests/AI/AnalyticResponseTests.cs a partir de specs/001-validar-arquivo-dimp/contracts/llm-analysis.schema.json: additionalProperties=false, todos os campos required, IDs existentes, recusa/truncamento e proteção dos fatos determinísticos. (Depende de: T020.)
- [ ] T084 [P] [US3] Escrever testes de transporte e injeção de instruções em tests/AgenteDIMP.IntegrationTests/US3/ExternalPayloadPrivacyTests.cs com identificadores sintéticos conhecidos; conferir minimização e mascaramento em prompts, respostas de ferramentas, retentativas, persistência e logs. (Depende de: T020.)
- [ ] T085 [US3] Criar Document/DocumentRevision, KnowledgeChunk, EmbeddingProfile, ChunkVector, IngestionJob e RetrievalEvaluation em src/Api.RAG/Domain/Knowledge/KnowledgeModels.cs com "SHA-256 único", "Unique em revisionHash + perfis + chunkHash evita duplicação por reingestão." e "Outro perfil de dimensão ou revisão não participa da consulta."; preservar nulabilidade/localização sem inventar metadados. (Depende de: T081.)
- [ ] T086 [US3] Criar esquema/migrações do acervo em src/Api.RAG/Infrastructure/Persistence/KnowledgeDbContext.cs e src/Api.RAG/Infrastructure/Persistence/Migrations/, com pgvector e índices de revisão/layout/registro; documento revisado gera nova revisão em vez de sobrescrever referência existente. (Depende de: T085.)
- [ ] T087 [US3] Implementar extração PDF por página/palavra com PdfPig em src/Api.RAG/Infrastructure/Ingestion/PdfDocumentExtractor.cs e fixtures em tests/fixtures/rag/; provar preservação de localizadores e revisão das tabelas, sem confiar na ordem linear do texto como leitura visual. (Depende de: T085.)
- [ ] T088 [US3] Construir conjunto de avaliação recuperável em tests/fixtures/rag/evaluation.json com perguntas por registro/campo/tabela, revisão errada e ausência de resposta, associando páginas/trechos esperados revisados; resultados esperados não podem ser gerados pelo mesmo algoritmo avaliado. (Depende de: T022, T087.)
- [ ] T089 [US3] Implementar segmentação orientada a seção/registro e cabeçalhos de tabelas em src/Api.RAG/Application/Ingestion/DocumentChunker.cs com perfis versionados de extração/segmentação; não fixar chunk size/overlap/topK/limiar sem experimento. (Depende de: T087, T088.)
- [ ] T090 [US3] Implementar embeddings em src/Api.RAG/Infrastructure/AI/EmbeddingService.cs com modelo/dimensão externos, baseline text-embedding-3-small/1536, ledger pré-chamada e novo perfil/índice em mudança de dimensão; modo fake separado do real. (Depende de: T013, T014, T086, T089.)
- [ ] T091 [US3] Implementar ingestão idempotente por hash e perfis em src/Api.RAG/Application/Ingestion/DocumentIngestionService.cs e src/Api.RAG/Workers/DocumentIngestionWorker.cs, com fila própria por ingestionJobId, lease/fencing, checkpoints por chunk e deduplicação concorrente por revisão/perfis conforme data-model.md; publicar somente corpus completo, preservar tentativa unknown sem retry automático após resposta perdida, sourceUrl apenas como proveniência e sem download arbitrário de URL enviada; acrescentar cenários de crash/concorrência em tests/AgenteDIMP.IntegrationTests/US3/IngestionRecoveryTests.cs. (Depende de: T086, T089, T090.)
- [ ] T092 [US3] Implementar POST ingestão, GET status e GET revisões em src/Api.RAG/Endpoints/DocumentIngestionEndpoints.cs conforme specs/001-validar-arquivo-dimp/contracts/rag.openapi.json, com DTOs completos, revisão imutável e erros sanitizados. (Depende de: T081, T091.)
- [ ] T093 [US3] Implementar recuperação exata pgvector em src/Api.RAG/Infrastructure/Retrieval/ExactVectorSearch.cs com filtros de documentRevisionSha256/layoutVersion/embeddingProfileId e contexto de registro quando disponível; similaridade não é confiança fiscal. (Depende de: T086, T090.)
- [ ] T094 [US3] Executar comparação reproduzível dos perfis em tests/AgenteDIMP.IntegrationTests/US3/RetrievalEvaluationTests.cs e registrar perfil selecionado, parâmetros medidos e limites em docs/validation/rag-evaluation.md; somente promover perfil aprovado no gate objetivo de research.md, usando calibração e aceite separados, 100% das evidências obrigatórias recuperadas dentro do orçamento, referências válidas, zero mistura de revisão e todos os casos sem evidência tratados; nenhum vencedor reprovado pode ser promovido. (Depende de: T088, T089, T093.)
- [ ] T095 [US3] Implementar servidor MCP HTTP stateless em src/Api.RAG/Infrastructure/Mcp/ManualTools.cs com entradas/saídas e erros de specs/001-validar-arquivo-dimp/contracts/rag-mcp.md e rag-mcp.schema.json, hashes/perfis validados e proveniência; verificar combinação estável SDK/protocolo prevista no plano sem copiar handshake legado. (Depende de: T082, T092, T094.)
- [ ] T096 [US3] Implementar cliente MCP tipado em src/Api.Agente/Infrastructure/Rag/RagMcpClient.cs fixando filtros pelo host, propagando cancelamento/orçamento e retornando ausência de evidência explicitamente; não usar REST administrativo ou conexão direta ao banco rag. (Depende de: T082, T095.)
- [ ] T097 [US3] Implementar adaptadores nativos SK e laço manual em src/Api.Agente/Infrastructure/AI/ManualRetrievalFunctions.cs permitindo somente ferramentas de leitura previstas; validar argumentos, não deixar o modelo trocar revisão/perfil nem ampliar catálogo de ferramentas. (Depende de: T096.)
- [ ] T098 [US3] Implementar MongoDB.Driver/MongoClient compartilhado e coleção conversation_contexts em src/Api.Agente/Infrastructure/Memory/MongoConversationContextStore.cs com "Índice único de executionId/conversationId impede contextos duplicados." e "Índice TTL de campo único em expiresAt usa expireAfterSeconds=0."; guardar apenas contexto mascarado, filtrar expiresAt na leitura e usar configuração externa Atlas/local. (Depende de: T014, T016, T084.)
- [ ] T099 [US3] Testar MongoDB real isolado em tests/AgenteDIMP.IntegrationTests/US3/MongoContextTests.cs com separação por execução, expiração lógica e exclusão explícita confirmada ao finalizar; TTL assíncrono não substitui Delete por executionId, não criar histórico de chat fora do escopo. (Depende de: T098.)
- [ ] T100 [US3] Implementar preparo de contexto limitado em src/Api.Agente/Application/AI/AnalysisContextBuilder.cs usando resumos e trechos necessários mascarados, referências reais e contexto MongoDB; nenhuma chamada envia original completo ou mapa reversível e nenhum achado determinístico é descartado para caber no prompt. (Depende de: T096, T099.)
- [ ] T101 [US3] Implementar conector OpenAI/SK em src/Api.Agente/Infrastructure/AI/OpenAiAnalysisClient.cs com modelo externo, baseline gpt-6-luna/reasoning_effort=none nas ferramentas, chamada final sem ferramentas com schema estrito e validação de capacidades; desabilitar retries ocultos, tratar recusa e registrar cada tentativa antes de chamar. (Depende de: T013, T014, T083, T097, T100.)
- [ ] T102 [US3] Implementar validador semântico e pipeline de reparo limitado em src/Api.Agente/Application/AI/AnalyticResponseValidator.cs, rejeitando findingIds/evidenceIds fora da execução, referência inexistente e alteração de fatos; revisão humana do sistema é OR das condições obrigatórias. (Depende de: T083, T101.)
- [ ] T103 [US3] Integrar geração contextual e degradação controlada ao worker em src/Api.Agente/Application/AI/ContextualAnalysisStage.cs, marcando grupos não analisados quando o orçamento acabar e preservando relatório determinístico em falha RAG/IA; conectar limpeza confirmada da memória MongoDB ao finalizador. (Depende de: T079, T099, T100, T102.)
- [ ] T104 [US3] Executar aceitação independente US3 com doubles, fixtures e falhas em tests/AgenteDIMP.IntegrationTests/US3/ContextualAnalysisTests.cs, cobrindo citação recuperável, nenhum vazamento, nenhuma ferramenta de escrita e relatório parcial; registrar evidências em docs/validation/us3-ai-rag.md. (Depende de: T084, T103.)
- [ ] T105 [US3] Criar teste opt-in Category=LiveProvider em tests/AgenteDIMP.IntegrationTests/US3/LiveProviderCompatibilityTests.cs para a combinação exata SK/SDK/modelo/effort/schema e embeddings; executar somente com credencial externa e dados sintéticos/mascarados, registrando capacidade/consumo real em docs/validation/live-provider.md. (Depende de: T094, T101, T102, T104.)

**Checkpoint**: Análise contextual verificável com doubles; qualidade RAG requer perfil avaliado e capacidade real do provedor requer T105.

## Phase 6: User Story 4 - Rastrear a execução e seu consumo (Priority: P3)

**Goal**: Consolidar consumo/custos confiáveis, histórico mascarado e provas do ciclo de vida sem reter o arquivo original.

**Independent Test**: Usar ledger e relógio controláveis para verificar retentativas, métricas desconhecidas, soma sem duplicação, exclusão antes de encerramento e fronteira de 30 dias; pode usar resultados pré-construídos.

Testes e inventário aparecem antes da implementação correspondente; tarefas de modelos/serviços/endpoints seguem suas dependências.

- [ ] T106 [P] [US4] Escrever testes de ledger e cálculo de custos em tests/Api.Agente.UnitTests/Usage/UsageAccountingTests.cs com métricas ausentes/parciais, tokens em cache, tentativa com transporte ambíguo e pricebook indisponível; nulo nunca vira zero. (Depende de: T020.)
- [ ] T107 [P] [US4] Escrever testes temporais/ciclo de vida em tests/AgenteDIMP.IntegrationTests/US4/RetentionBoundaryTests.cs com relógio falso, falha/cancelamento/timeout e limpeza interrompida; relatório acessível antes de 30 dias e 410 ao completar o prazo, sem depender de esperar o TTL do MongoDB. (Depende de: T020.)
- [ ] T108 [P] [US4] Escrever testes de correlação entre serviços em tests/AgenteDIMP.IntegrationTests/US4/CrossServiceUsageTests.cs, provando que embeddings da consulta/ingestão pertencem ao ledger rag e não são somados duas vezes nem lidos diretamente pelo banco agente. (Depende de: T020.)
- [ ] T109 [US4] Completar PriceBookVersion e cálculo decimal em src/Api.Agente/Infrastructure/Usage/PriceBookCostEstimator.cs com fonte, consulta, vigência, moeda, unidade e tarifas; preservar "Tarifa ausente produz custo desconhecido, nunca zero." e distinguir estimado de informado pelo provedor. (Depende de: T013, T106.)
- [ ] T110 [US4] Implementar política única de tentativas em src/Api.Agente/Infrastructure/Usage/ProviderAttemptScope.cs e src/Api.RAG/Infrastructure/Usage/ProviderAttemptScope.cs, reconciliando estado antes/depois da chamada, 3 tentativas transitórias totais, 1 reparo e orçamento global; verificar contagem real do transporte sem retries ocultos. (Depende de: T013, T014, T106.)
- [ ] T111 [US4] Implementar get_ingestion_usage e referências de consumo por consulta em src/Api.RAG/Infrastructure/Mcp/UsageTools.cs e src/Api.RAG/Infrastructure/Retrieval/RetrievalUsageEnvelope.cs, preservando métricas anuláveis, callAttemptId e dono do serviço; não expor segredos. (Depende de: T090, T095, T108, T110.)
- [ ] T112 [US4] Agregar métricas locais e referências MCP em src/Api.Agente/Application/Usage/ExecutionUsageAggregator.cs, com correlação por tentativa e sem duplicação; expor hasUnknownUsage e custo estimado/preço referenciado no envelope do sistema, nunca no JSON gerado pelo LLM. (Depende de: T109, T110, T111.)
- [ ] T113 [US4] Implementar ExecutionTombstone e expiração em src/Api.Agente/Infrastructure/Reports/ReportExpirationService.cs e src/Api.Agente/Workers/RetentionWorker.cs com "Mantido por 90 dias após expiração; durante esse prazo consulta retorna 410." e "Após purgeAt o identificador pode retornar 404."; coletor inicial de 1 minuto não atrasa a expiração lógica de 30 dias. (Depende de: T077, T078, T107.)
- [ ] T114 [US4] Implementar retenção operacional em src/Api.Agente/Workers/UsageRetentionWorker.cs e src/Api.RAG/Workers/UsageRetentionWorker.cs preservando "Retenção operacional de tentativas: 90 dias após a chamada, sem conteúdo bruto." e "Pricebook referenciado permanece enquanto houver tentativas que o utilizem."; logs sanitizados de desenvolvimento por 7 dias. (Depende de: T109, T112.)
- [ ] T115 [US4] Completar varredura de órfãos, limpeza de contexto e alertas em src/Api.Agente/Workers/ArtifactRecoveryWorker.cs, incluindo original, sidecars, mapa e MongoDB por executionId; só encerrar após exclusão confirmada, recuperar leases/crashes e não guardar dados brutos por atalhos no ledger/tombstone. (Depende de: T018, T070, T099, T107.)
- [ ] T116 [US4] Integrar consumo, expiração e recuperação ao relatório em src/Api.Agente/Application/Reports/BuildExecutionReport.cs e registrar teste ponta a ponta US4 em tests/AgenteDIMP.IntegrationTests/US4/ExecutionAuditTests.cs, com retentativas reais observáveis, métricas desconhecidas e referências recuperáveis durante a retenção. (Depende de: T079, T108, T112, T113, T114, T115.)
- [ ] T117 [US4] Executar aceitação US4 e registrar evidências de limpeza/expiração/consumo em docs/validation/us4-audit-retention.md, incluindo testes de datas de fronteira e estado de fonte desconhecida sem métricas fabricadas. (Depende de: T116.)

**Checkpoint**: Consumo, correlação e retenção verificáveis por relógio/falhas controlados, inclusive exclusão MongoDB confirmada.

## Phase 7: Polish & Cross-Cutting Concerns

**Goal**: Consolidar aceite das quatro jornadas, documentação operacional e evidências sem ampliar o escopo para deploy/A2A/autorização.

**Independent Test**: Roteiro reproduzível, rastreabilidade dos 32 requisitos e 13 critérios, nenhuma alegação de capacidade ou cobertura sem evidência.

- [ ] T118 Executar o roteiro completo de specs/001-validar-arquivo-dimp/quickstart.md contra a solução implementada, primeiro offline e com dados sintéticos menores; registrar comandos/resultados reais em docs/validation/quickstart-results.md, sem afirmar sucesso por apenas criar arquivos. (Depende de: T071, T080, T104, T117.)
- [ ] T119 Medir bytes, registros, duração por etapa, pico de memória e custos nos exemplos menores em tests/AgenteDIMP.IntegrationTests/Performance/SmallFileBaselineTests.cs e docs/validation/performance-baseline.md; registrar limites efetivos de host e manter teto de produto indefinido sem extrapolar para arquivos grandes. (Depende de: T118.)
- [ ] T120 Verificar não exposição de dados e fronteiras de ferramentas em tests/AgenteDIMP.IntegrationTests/Security/OutputAndToolBoundaryTests.cs, cobrindo payloads, respostas, logs, MongoDB, PostgreSQL, temporários e ausência de efeitos de escrita; preservar autorização adiada em EX-001. (Depende de: T118.)
- [ ] T121 Produzir matriz requisito→tarefa→teste→evidência em docs/validation/traceability.md para FR-001 a FR-032 e SC-001 a SC-013; incluir todos os contratos e revisar a matriz fonte→regras com zero omissões antes de declarar cobertura completa. (Depende de: T071, T080, T104, T117, T119, T120.)
- [ ] T122 Atualizar README.md, docs/configuration.md e specs/001-validar-arquivo-dimp/quickstart.md com execução local, MongoDB Atlas/local, PostgreSQL/pgvector, migrações explícitas, orçamentos, limpeza e operação; A2A, frontend, autorização e deploy Vercel continuam fora do marco aprovado. (Depende de: T121.)
- [ ] T123 Concluir revisão constitucional e de contratos em docs/validation/release-readiness.md, verificando EX-001, size limit adiado, resolução oficial de SF-001/002/003 e compatibilidade real do provedor quando testada; manter pendentes as tarefas sem evidência e não publicar ou fazer push automaticamente. (Depende de: T105, T118, T121, T122.)

**Checkpoint**: Entrega só é aceita com evidência dos critérios e fonte; tarefas sem comprovação continuam abertas.

## Dependencies & Execution Order

### Phase Dependencies

~~~text
Setup T001–T006
  -> Foundational T007–T020
      -> US1: catálogo -> testes -> parser -> 29 validadores -> worker -> consultas
      -> US2: testes com resultados construídos -> políticas -> composição
      -> US3: testes -> acervo -> pgvector/MCP -> MongoDB/SK/LLM -> análise
      -> US4: testes com ledger/relógio -> custos -> retenção/recuperação
US1 consulta básica T069 -> integração US2 T078/T079
US2 composição T079 + cliente/contexto US3 -> integração contextual T103
Acervo/MCP US3 + ledger fundamental -> correlação US4 T111/T112
Todas as jornadas -> validação integrada T118–T123

Fonte oficial SF-001/002/003 -> T023/T024/T025 -> T064 -> T071 -> gates finais
~~~

O grafo completo é expresso também em cada linha de tarefa.
A sequência dos IDs não obriga parar em uma tarefa impedida se houver outra cujas
dependências estejam concluídas. A fundação bloqueia todas as jornadas;
resolução de fonte bloqueia somente trabalho/aceite dependente dessa interpretação.

### User Story Dependencies

- US1: depende da fundação; testes determinísticos dispensam RAG e LLM.
- US2: políticas/composição podem ser desenvolvidas contra resultados construídos
  após a fundação; a ligação HTTP real depende da consulta básica de US1(T069).
- US3: acervo/cliente/modelo são testáveis com fixtures e doubles após a fundação;
  integração com o worker real depende da composição US2(T079).
- US4: custos/tempo são testáveis com ledger/relógio construídos; a correlação real
  depende dos contratos MCP e a limpeza de memória depende do store MongoDB(T099).
- Nenhuma independência de teste elimina dependências de integração.
- Demonstrações intermediárias não substituem o escopo final com as quatro jornadas.

### Within Each User Story

1. Definir oráculos/fixtures e testes verificáveis antes dos componentes correspondentes.
2. Implementar modelos e contratos antes de serviços e endpoints.
3. Validar casos de falha e isolamento, não somente o caminho feliz.
4. Registrar evidências e preservar tarefas bloqueadas/sem teste como abertas.
5. Rever o trabalho com os artefatos SDD quando uma descoberta alterar requisito ou arquitetura.

### Parallel Opportunities

Existem 47 tarefas com [P].
Os grupos abaixo usam arquivos distintos após os mesmos pré-requisitos; não são
autorização para executar agentes nem operações remotas por conta própria.

## Parallel Example: User Story 1

- Após T022, investigar T023/T024/T025 em três arquivos de conflitos distintos.
- Após T026, escrever T027/T028/T029 em suítes distintas.
- Após T034, T035–T063 permitem implementar validadores e testes de registros separados.
  Não alterar registro central/engine em paralelo; T065 faz essa integração.
- Uma regra de registro com fonte impedida continua bloqueada no catálogo até T064.

## Parallel Example: User Story 2

Após T020, T072/T073/T074 podem preparar testes de contrato, política e envelope
em arquivos distintos. Composição e endpoints aguardam esses testes e as dependências
indicadas; dois trabalhos no mesmo GetAnalysisReportEndpoint.cs não são paralelos.

## Parallel Example: User Story 3

Após T020, T081/T082/T083/T084 escrevem testes de ingestão, MCP, resposta analítica e
privacidade em arquivos separados. O pipeline de acervo e o store MongoDB podem avançar
por suas dependências próprias; apenas T103 integra os componentes à execução real.

## Parallel Example: User Story 4

Após T020, T106/T107/T108 preparam testes de custos, retenção e correlação sem editar
os mesmos arquivos. Integração e limpeza dependem dos contratos/store prontos;
não presumir que um perfil de teste já representa a capacidade real do provedor.

## Implementation Strategy

### MVP First (primeiro marco técnico de US1)

Concluir Setup/Foundation e demonstrar submissão assíncrona + diagnóstico determinístico
de US1 com arquivos sintéticos menores. Esse marco NÃO reduz o escopo aprovado para
todas as regras e fluxo completo, nem autoriza disponibilização compartilhada/produção.

Enquanto existirem conflitos oficiais, a demonstração precisa expor as regras
impedidas e permanecer parcial. A entrega completa exige US1, US2, US3 e US4,
incluindo retenção, privacidade e evidências.

### Incremental Delivery

1. Preparar ambiente e fundação sem operações remotas automáticas.
2. Inventariar fonte completa e separar conflitos; implementar regras sem conflito.
3. Demonstrar US1 e consolidar relatório/semântica de US2.
4. Implementar acervo/MCP, memória MongoDB e análise de US3.
5. Completar correlação/custos e retenção/recuperação em US4.
6. Validar roteiro, matriz de cobertura e gates finais.
7. Antes de implementar, executar speckit-analyze para revisar consistência desta lista.
   Não marcar itens concluídos apenas porque o tasks.md foi gerado.

## Matriz de cobertura dos requisitos

| Requisitos | Tarefas principais |
| --- | --- |
| FR-001, FR-002, FR-003, FR-004 | T014, T026–T033, T066–T070, T119 |
| FR-005, FR-006, FR-007 | T021–T065, T071, T121 |
| FR-008, FR-014, FR-015, FR-016, FR-017, FR-018, FR-022, FR-023 | T010–T011, T072–T080, T102–T104 |
| FR-009, FR-010, FR-012, FR-026 | T081–T097, T104, T120 |
| FR-011, FR-013, FR-027 | T015–T016, T083–T084, T098–T105 |
| FR-019, FR-020 | T013–T014, T090, T106–T112, T114, T116–T117 |
| FR-021, FR-024 | T020, T097, T104, T120, T123 |
| FR-025 | T008–T012, T015–T019, T066–T070, T099, T116 |
| FR-028, FR-029 | T017–T019, T077–T079, T098–T099, T107, T113–T117 |
| FR-030, FR-031, FR-032 | T027, T066–T070, T072, T078, T107, T113 |

| Critério | Evidência planejada |
| --- | --- |
| SC-001, SC-002, SC-003, SC-009 | T026–T034, T035–T065, T071, T073–T080, T121 |
| SC-004, SC-005, SC-008 | T073–T080, T082–T084, T094–T105 |
| SC-006 | T106–T112, T116–T117 |
| SC-007 | T019, T070, T099, T108, T116 |
| SC-010 | T015–T016, T084, T098–T104, T120 |
| SC-011, SC-012 | T018–T019, T099, T107, T113–T117 |
| SC-013 | T027, T066–T070, T072, T078, T118 |

## Resumo da geração

| Fase | Quantidade |
| --- | --- |
| Setup | 6 |
| Foundational | 14 |
| US1 — validação determinística | 51 |
| US2 — relatório e classificação | 9 |
| US3 — RAG, memória e IA | 25 |
| US4 — consumo e ciclo de vida | 12 |
| Polish | 6 |
| Total | 123 |

## Notes

- Nenhuma tarefa foi executada nesta geração; todos os checkboxes permanecem abertos.
- Arquivos/caminhos de implementação são entregáveis planejados, não arquivos já existentes.
- O validador de formato desta lista não substitui build/testes nem revisão fiscal.
- As skills github-versionamento e vercel-dotnet-pgvector-atlas permanecem instaladas,
  mas esta etapa não executa seus fluxos.
- A2A ativo, autorização e deploy exigem marcos posteriores já definidos nos artefatos.
