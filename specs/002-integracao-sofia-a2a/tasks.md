# Tarefas de Implementação: Integração SOFIA A2A e CORS

**Feature:** `002-integracao-sofia-a2a`  
**Status:** Em Execução  

---

## Fase 1: Modelos e Contratos de Dados (SOFIA v1.0 e A2A v0.3.x)

- [x] **T01-1**: Criar `SofiaContracts.cs` em `src/Api.Agente/Application/Sofia/` com DTOs `SolicitacaoAnaliseSofia`, `ResultadoAnaliseSofia`, `AchadoSofia`, `UsuarioSofia`, `DocumentoSofia`, `ContextoSofia`, `AgenteSofia`, `ErroSofia` com validação de invariantes e anotações.
- [x] **T01-2**: Criar `A2AContracts.cs` em `src/Api.Agente/Application/Sofia/` com DTOs para envelope JSON-RPC 2.0 `message/send`, `Task`, `Artifact` e `TextPart`.
- [x] **T01-3**: Criar suíte de testes unitários `SofiaContractTests.cs` em `tests/Api.Agente.UnitTests/Sofia/` validando serialização, round-trip e rejeição de versão diferente de `"1.0"`.

---

## Fase 2: Configuração CORS e Agent Card

- [x] **T02-1**: Configurar a política CORS `"SofiaBrowser"` em `FoundationRegistration.cs` / `Program.cs` e adicionar `Cors:AllowedOrigins` em `appsettings.json` e `appsettings.Development.json`.
- [x] **T02-2**: Criar endpoint `AgentCardEndpoint.cs` em `src/Api.Agente/Endpoints/` expondo `GET /.well-known/agent-card.json`.
- [x] **T02-3**: Criar testes para o Agent Card e preflight CORS (`OPTIONS`) para origens autorizadas e não autorizadas.

---

## Fase 3: Handler de Análise SOFIA (Integração com Validação DIMP)

- [x] **T03-1**: Criar interface `ISofiaAnalysisHandler` e implementação `SofiaAnalysisHandler` em `src/Api.Agente/Application/Sofia/`.
- [x] **T03-2**: Implementar inspeção de documentos e execução de `DimpParser` e `DimpValidationEngine` em memória para arquivos DIMP.
- [x] **T03-3**: Implementar mapeamento de `Finding` para `AchadoSofia` e compor `ResultadoAnaliseSofia` estruturado.
- [x] **T03-4**: Tratar documentos não compatíveis com formato DIMP e casos de documentos sem conteúdo com erro controlado.
- [x] **T03-5**: Criar testes unitários para `SofiaAnalysisHandler` cobrindo cenários com e sem divergências fiscais.

---

## Fase 4: Endpoint A2A e Mapeamento de Protocolo

- [x] **T04-1**: Criar endpoint `A2AEndpoint.cs` em `src/Api.Agente/Endpoints/` mapeando `POST /a2a`.
- [x] **T04-2**: Implementar validação do envelope JSON-RPC 2.0 (`jsonrpc: "2.0"`, `method: "message/send"`), extração do payload `SolicitacaoAnaliseSofia`, invocação do handler e retorno com `Task completed` e `Artifact`.
- [x] **T04-3**: Registrar endpoints no `Program.cs` com middleware CORS e injeção de dependências.

---

## Fase 5: Testes de Integração e Verificação End-to-End

- [x] **T05-1**: Criar testes de integração `A2AEndpointTests.cs` simulando chamadas completas do orquestrador SOFIA.
- [x] **T05-2**: Testar caso de documento DIMP com divergências fiscais, documento sem divergências, documento com erro e documento com layout não-DIMP.
- [x] **T05-3**: Executar toda a suíte de testes do repositório para garantir 100% de aprovação e nenhuma regressão.

---

## Fase 6: Documentação, Evidências e Publicação Git

- [x] **T06-1**: Criar relatório de evidência de validação A2A em `docs/validation/sofia-a2a-integration.md`.
- [x] **T06-2**: Atualizar `README.md` com instruções de consumo A2A e especificação dos endpoints.
- [x] **T06-3**: Criar commit Git padronizado e realizar `git push origin main`.
