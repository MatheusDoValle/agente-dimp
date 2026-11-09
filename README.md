# Agente DIMP - Validador de Arquivos DIMP com IA e RAG

Duas APIs construídas em ASP.NET Core e .NET 10:
- **`Api.Agente`**: Orquestração assíncrona, validação determinística fiscal, análise contextual por IA, gerenciamento de relatórios e memória conversacional MongoDB.
- **`Api.RAG`**: Ingestão oficial do Manual CONFAZ (PDF), segmentação por seção/registro, busca vetorial exata com PostgreSQL PgVector e exposição via Model Context Protocol (MCP) stateless no endpoint `/mcp`.

Repositório Oficial: https://github.com/MatheusDoValle/agente-dimp

---

## 1. Visão Geral das Quatro Jornadas Implementadas

1. **User Story 1 - Validação Determinística:**
   - 29 regras fiscais implementadas e validadas para o leiaute DIMP V10.
   - Leitura de arquivos por streaming contínuo (`IAsyncEnumerable<DimpRecord>`) com memória delimitada.
   - Filas de processamento duráveis com persistência de checkpoints e fencing tokens no PostgreSQL (`processing_jobs`).

2. **User Story 2 - Relatórios e Achados Estruturados:**
   - Composição de relatórios com categorização estrita em 5 tipos (`erro_estrutural`, `inconsistencia_negocio`, `indicio_ia`, `ausencia_evidencias`, `falha_tecnica`).
   - Autoridade inegociável do envelope do sistema sobre o veredito, severidades e exigência de revisão humana.
   - Paginação estável de achados orientada a cursor base64 (`GET /api/v1/analises/{id}/achados`).

3. **User Story 3 - RAG, MCP e Análise Contextual com IA:**
   - Extração estruturada de manuais PDF com PdfPig, segmentação por registros/tabelas e cálculo determinístico de hashes.
   - Servidor MCP stateless em `Api.RAG` (`/mcp`) e cliente tipado `RagMcpClient` na `Api.Agente` (**zero acesso SQL direto** ao banco `rag` pela `Api.Agente`).
   - Minimização estrita de dados sensíveis (`ContentSanitizer`), memória contextual MongoDB (`conversation_contexts`) e degradação graciosa em caso de indisponibilidade de IA.

4. **User Story 4 - Custos, Ledger e Ciclo de Vida:**
   - Registro de chamadas pré-chamada em ledger PostgreSQL; nulo nunca vira zero em métricas ausentes.
   - Catálogo de preços versionado (`PriceBookVersion`) e cálculo de precisão decimal `(28, 12)` sem retries ocultos (`ProviderAttemptScope`).
   - Retenção do relatório mascarado por 30 dias (expiração imediata retornando `410 Gone`).
   - `ExecutionTombstone` mantido por 90 dias após expiração (durante os quais retorna `410 Gone`; após expurgo do tombstone passa a retornar `404 Not Found`).
   - Limpeza e recuperação garantida de arquivos temporários e contexto MongoDB (`ArtifactRecoveryWorker`).

5. **Integração SOFIA A2A e CORS (Feature 002):**
   - Servidor padronizado **Agent2Agent (A2A) v0.3.x JSON-RPC 2.0** no endpoint `POST /a2a`.
   - Publicação de **Agent Card** para descoberta automática em `GET /.well-known/agent-card.json`.
   - Contrato de domínio **SOFIA v1.0** com validação de conformidade DIMP via `SofiaAnalysisHandler`.
   - Política de **CORS** (`SofiaBrowser`) com suporte a preflight `OPTIONS` para origens web autorizadas.

---

## 2. Compilação e Execução de Testes

### 2.1 Compilar a Solução

```powershell
dotnet restore ./AgenteDIMP.slnx --locked-mode
dotnet build ./AgenteDIMP.slnx --no-restore
```

### 2.2 Executar Testes Unitários e de Integração A2A (Offline)

```powershell
dotnet test ./tests/Api.Agente.UnitTests/Api.Agente.UnitTests.csproj
dotnet test ./tests/Api.RAG.UnitTests/Api.RAG.UnitTests.csproj
dotnet test ./tests/AgenteDIMP.IntegrationTests/AgenteDIMP.IntegrationTests.csproj --filter "FullyQualifiedName~A2AIntegrationTests"
```

Total: **230+ testes aprovados, 0 falhas, 0 ignorados**.

### 2.3 Executar Testes de Integração com Banco Local

Quando os contêineres Docker locais estiverem iniciados:

```powershell
if (-not (Test-Path ./infra/.env)) { ./infra/New-LocalEnvironment.ps1 }
docker compose --env-file ./infra/.env -f ./infra/compose.yaml up -d --wait
./infra/Test-Foundation.ps1 -All -NoBuild
```

---

## 3. Execução das APIs em Modo LocalDeterministic

Em dois terminais PowerShell separados:

```powershell
# Terminal 1 - Servidor RAG / MCP (Porta 5081)
dotnet run --project ./src/Api.RAG --launch-profile LocalDeterministic -- --urls http://127.0.0.1:5081

# Terminal 2 - Agente / Orquestrador (Porta 5080)
dotnet run --project ./src/Api.Agente --launch-profile LocalDeterministic -- --urls http://127.0.0.1:5080
```

Verificar integridade dos serviços:
- `http://127.0.0.1:5081/health/live`
- `http://127.0.0.1:5080/health/live`

---

## 4. Documentação de Validação e Rastreabilidade

- [Roteiro de Validação e Quickstart](specs/001-validar-arquivo-dimp/quickstart.md)
- [Resultados do Quickstart](docs/validation/quickstart-results.md)
- [Matriz de Rastreabilidade (FR-001 a FR-032 e SC-001 a SC-013)](docs/validation/traceability.md)
- [Evidência US1 - Validação Determinística](docs/validation/us1-deterministic.md)
- [Evidência US2 - Relatórios e Achados](docs/validation/us2-reporting.md)
- [Evidência US3 - Apoio do Manual e da IA](docs/validation/us3-ai-rag.md)
- [Evidência US4 - Custos, Ledger e Retenção](docs/validation/us4-audit-retention.md)
- [Linha de Base de Desempenho](docs/validation/performance-baseline.md)
- [Configuração de Ambientes e Operação](docs/configuration.md)
- [Evidência de Integração SOFIA A2A e CORS](docs/validation/sofia-a2a-integration.md)
- [Prontidão para Liberação (Release Readiness)](docs/validation/release-readiness.md)
