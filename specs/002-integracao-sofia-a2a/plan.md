# Plano de Implementação: Integração SOFIA A2A e CORS

**Feature:** `002-integracao-sofia-a2a`  
**Data:** 2026-11-09  
**Status:** Aprovado  

---

## 1. Arquitetura da Solução

```text
               +-------------------------------------------------+
               |              SOFIA (Orquestrador)               |
               +-------------------------------------------------+
                         |                             |
      1. Descoberta      |                             | 2. Tarefa A2A
      GET /.well-known/  |                             | POST /a2a (JSON-RPC)
      agent-card.json    v                             v
  +-----------------------------------------------------------------------+
  |                     Api.Agente (Servidor A2A)                         |
  |                                                                       |
  |  [CORS Middleware: SofiaBrowser]                                      |
  |                                                                       |
  |  +---------------------------+       +-----------------------------+  |
  |  |   AgentCardEndpoint       |       |       A2AEndpoint           |  |
  |  |  (/.well-known/agent-card)|       |         (/a2a)              |  |
  |  +---------------------------+       +-----------------------------+  |
  |                                                     |                 |
  |                                                     v                 |
  |                                      +-----------------------------+  |
  |                                      |   SofiaAnalysisHandler      |  |
  |                                      +-----------------------------+  |
  |                                                     |                 |
  |                     +-------------------------------+                 |
  |                     v                               v                 |
  |        +-------------------------+    +--------------------------+    |
  |        |       DimpParser        |    |   DimpValidationEngine   |    |
  |        +-------------------------+    +--------------------------+    |
  +-----------------------------------------------------------------------+
```

---

## 2. Decisões Arquiteturais (ADRs)

### ADR-01: Transporte A2A v0.3.x e JSON-RPC 2.0
- **Contexto:** O orquestrador SOFIA e os agentes especializados adotam o protocolo Agent2Agent (A2A) v0.3.x sobre JSON-RPC 2.0.
- **Decisão:** O AgenteDIMP disponibilizará o endpoint `POST /a2a` processando requisições JSON-RPC `method: "message/send"`.
- **Justificativa:** Compatibilidade com a especificação acordada entre todos os agentes do projeto SOFIA da INFIS, sem inventar rotas REST proprietárias.

### ADR-02: Separação de Transporte e Domínio de Negócio
- **Contexto:** Protocolo A2A lida com roteamento e envelopes de mensagens; o domínio tributário da SOFIA é definido pelo contrato `versaoContrato: "1.0"`.
- **Decisão:** Os modelos `SolicitacaoAnaliseSofia` e `ResultadoAnaliseSofia` trafegam estritamente serializados como JSON UTF-8 dentro de `parts[].text` (`kind: "text"`).
- **Justificativa:** Respeita a especificação do perfil A2A v0.3.x e impede o acoplamento de propriedades de negócio nos envelopes de transporte.

### ADR-03: Processamento Determinístico Direto via Handler
- **Contexto:** Documentos submetidos pela SOFIA contêm textos extraídos (como arquivos DIMP integrais ou trechos fiscais).
- **Decisão:** `SofiaAnalysisHandler` realiza parsing assíncrono em memória usando o `DimpParser` e executa o `DimpValidationEngine`. Se o documento contiver leiaute DIMP válido, todas as regras determinísticas do Ato COTEPE 65/2018 são aplicadas, mapeando cada `Finding` para `AchadoSofia` com sua devida severidade e evidência. Se o documento não for DIMP, uma resposta analítica clara de incompatibilidade é retornada com `status: "concluido"` sem falha fatal.
- **Justificativa:** Reutilização total da infraestrutura de validação da Feature 001 com alta performance (< 50ms) e sem dependência obrigatória de workers assíncronos para análises de documentos em lote direto da SOFIA.

### ADR-04: Política de CORS Segura para Navegadores
- **Contexto:** Usuários e frontends da SOFIA (React, Angular, Vue) podem consultar o Agent Card ou interagir diretamente a partir do navegador.
- **Decisão:** Configurar política `"SofiaBrowser"` no ASP.NET Core .NET 10, lendo origens permitidas de `Cors:AllowedOrigins` em `appsettings.json`.
- **Justificativa:** Cumpre a exigência de preflight seguro com suporte a `OPTIONS`, prevenindo requisições não autorizadas sem bloquear integrações servidor-servidor.

---

## 3. Contratos de Dados

### 3.1. DTOs SOFIA v1.0 (`Application/Sofia/SofiaContracts.cs`)
- `SolicitacaoAnaliseSofia`:
  - `versaoContrato` (string, fixo em `"1.0"`)
  - `idExecucao` (Guid)
  - `idSolicitacao` (Guid)
  - `usuario` (`UsuarioSofia`: `nome`)
  - `pergunta` (string)
  - `documentos` (`IReadOnlyList<DocumentoSofia>`: `idDocumento`, `nome`, `tipo`, `texto`)
  - `contexto` (`ContextoSofia?`: `idioma`, `instrucoes`)
- `ResultadoAnaliseSofia`:
  - `versaoContrato` (string, fixo em `"1.0"`)
  - `idExecucao` (Guid)
  - `idSolicitacao` (Guid)
  - `agente` (`AgenteSofia`: `idAgente = "agente-dimp"`, `nome = "Agente Fiscal DIMP"`)
  - `status` (string: `"concluido"`, `"parcial"`, `"erro"`)
  - `analise` (string)
  - `achados` (`IReadOnlyList<AchadoSofia>`: `idAchado`, `descricao`, `idDocumentos`, `evidencias`, `severidade`)
  - `observacoes` (`IReadOnlyList<string>`)
  - `erro` (`ErroSofia?`: `codigo`, `mensagem`)

### 3.2. DTOs A2A v0.3.x (`Application/Sofia/A2AContracts.cs`)
- `A2ARpcRequest`: `jsonrpc: "2.0"`, `id: string`, `method: "message/send"`, `params: A2AMessageParams`.
- `A2ARpcResponse`: `jsonrpc: "2.0"`, `id: string`, `result: A2ATaskResult`.
- `A2ATaskResult`: `id: string`, `contextId: string`, `status: { state: "completed" }`, `artifacts: [ { artifactId, name, parts: [ { kind: "text", text } ] } ]`.
- `A2ARpcErrorResponse`: `jsonrpc: "2.0"`, `id: string`, `error: { code: int, message: string }`.

---

## 4. Estratégia de Testes

1. **Testes de Contrato SOFIA (`SofiaContractTests`):**
   - Serialização e desserialização de exemplos oficiais de `contrato-sofia-v1.md`.
   - Rejeição de contratos com versão diferente de `"1.0"`.
   - Validação de campos obrigatórios (`idExecucao`, `idSolicitacao`, `usuario.nome`, `documentos`).
2. **Testes do Handler de Análise (`SofiaAnalysisHandlerTests`):**
   - Execução com arquivo DIMP válido: `status = concluido`, 0 achados.
   - Execução com arquivo DIMP com inconsistência (ex.: campo ausente, soma de valores divergente): retorno de achados com severidade e evidência.
   - Execução com documento não-DIMP: resposta amigável sem exceção.
3. **Testes de Endpoint A2A e Agent Card (`A2AEndpointTests`):**
   - `GET /.well-known/agent-card.json`: retorno 200 com schema compatível.
   - `POST /a2a`: round-trip JSON-RPC 2.0 `message/send` com task completed.
   - Preflight `OPTIONS /a2a`: validação de cabeçalhos CORS `SofiaBrowser`.
   - Preflight `OPTIONS` com origem maliciosa: rejeição CORS.
