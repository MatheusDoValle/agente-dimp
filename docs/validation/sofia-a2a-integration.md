# Evidência de Integração: SOFIA A2A e CORS

**Data de Conclusão:** 2026-11-09  
**Feature:** `002-integracao-sofia-a2a`  
**Protocolo:** Agent2Agent (A2A) v0.3.x sobre JSON-RPC 2.0  
**Contrato SOFIA:** v1.0 (`versaoContrato: "1.0"`)  
**Status:** Validado e Aprovado  

---

## 1. Visão Geral da Arquitetura

O **Agente Fiscal DIMP INFIS** atua como servidor A2A padronizado para o ecossistema SOFIA da INFIS:
- **Descoberta:** Publicação de Agent Card em `GET /.well-known/agent-card.json` descrevendo capacidades e skill `analisar_documentos`.
- **Servidor A2A:** Endpoint `POST /a2a` processando envelopes JSON-RPC 2.0 com método `message/send`.
- **Contrato de Domínio SOFIA v1.0:** Payloads de negócio (`SolicitacaoAnaliseSofia` e `ResultadoAnaliseSofia`) trafegando estritamente em UTF-8 dentro de `parts[].text` com `kind: "text"`.
- **Segurança e CORS:** Política CORS `"SofiaBrowser"` restringindo acesso de navegador às origens cadastradas em `Cors:AllowedOrigins` (ex.: `http://localhost:4200`, `http://localhost:5173`), sem interferir nas conexões servidor-servidor HTTP na rede interna do Docker.

---

## 2. Endpoints Expostos

| Método | Rota | Descrição | Política CORS |
|---|---|---|---|
| `GET` | `/.well-known/agent-card.json` | Descoberta do agente pelo orquestrador SOFIA e clientes web | `SofiaBrowser` |
| `POST` | `/a2a` | Envio de mensagens e tarefas de análise A2A via JSON-RPC 2.0 | `SofiaBrowser` |
| `OPTIONS` | `/.well-known/agent-card.json`, `/a2a` | Preflight para clientes de navegador autorizados | `SofiaBrowser` |

---

## 3. Exemplos Reais de Envelopes

### 3.1. Agent Card (`GET /.well-known/agent-card.json`)

```json
{
  "name": "Agente Fiscal DIMP INFIS",
  "description": "Analisa e valida declaracoes e arquivos fiscais DIMP (Ato COTEPE/ICMS 65/2018) enviados pela SOFIA.",
  "url": "http://agente-dimp:8080/a2a",
  "version": "1.0.0",
  "capabilities": {
    "streaming": false,
    "pushNotifications": false
  },
  "defaultInputModes": ["text"],
  "defaultOutputModes": ["text"],
  "skills": [
    {
      "id": "analisar_documentos",
      "name": "Validacao Fiscal DIMP V10",
      "description": "Analisa arquivos DIMP e valida estrutura, totalizadores e regras de conformidade tributaria.",
      "tags": ["infis", "fiscal", "dimp", "sofia"]
    }
  ]
}
```

### 3.2. Chamada SOFIA → Agente Fiscal DIMP (`POST /a2a`)

```json
{
  "jsonrpc": "2.0",
  "id": "rpc-sofia-001",
  "method": "message/send",
  "params": {
    "message": {
      "role": "user",
      "messageId": "5f92c2ba-6870-4862-87ba-1ae8669a77f4",
      "parts": [
        {
          "kind": "text",
          "text": "{\"versaoContrato\":\"1.0\",\"idExecucao\":\"8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e\",\"idSolicitacao\":\"e1b3a4cb-d729-4bd4-b796-b9c94be07b46\",\"usuario\":{\"nome\":\"Maria Oliveira\"},\"pergunta\":\"Verifique divergencias fiscais no arquivo DIMP.\",\"documentos\":[{\"idDocumento\":\"doc-dimp-01\",\"nome\":\"remessa.dimp\",\"tipo\":\"text/plain\",\"texto\":\"|0000|10|4|SP|00000000000191|INSTITUICAO SINTETICA|20260601|20260630|2|202607|\\n|0001|1|\\n|0005|SINTETICA|RUA SINTETICA 1|01001000|3550308|SP|CONTATO SINTETICO|1100000000|exemplo@example.invalid|\\n|0990|4|\\n|1001|0|\\n|1990|2|\\n|9001|1|\\n|9900|0000|1|\\n|9900|0001|1|\\n|9900|0005|1|\\n|9900|0990|1|\\n|9900|1001|1|\\n|9900|1990|1|\\n|9900|9001|1|\\n|9900|9900|10|\\n|9900|9990|1|\\n|9900|9999|1|\\n|9990|13|\\n|9999|19|\"}]}"
        }
      ]
    }
  }
}
```

### 3.3. Resposta Agente Fiscal DIMP → SOFIA (`200 OK`)

```json
{
  "jsonrpc": "2.0",
  "id": "rpc-sofia-001",
  "result": {
    "id": "task-e1b3a4cbd729",
    "contextId": "ctx-8b2f0ad6c2e4",
    "status": {
      "state": "completed"
    },
    "artifacts": [
      {
        "artifactId": "art-a1b2c3d4e5f6",
        "name": "analise-sofia-v1",
        "parts": [
          {
            "kind": "text",
            "text": "{\"versaoContrato\":\"1.0\",\"idExecucao\":\"8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e\",\"idSolicitacao\":\"e1b3a4cb-d729-4bd4-b796-b9c94be07b46\",\"agente\":{\"idAgente\":\"agente-dimp\",\"nome\":\"Agente Fiscal DIMP\"},\"status\":\"concluido\",\"analise\":\"Arquivo DIMP 'remessa.dimp' analisado com sucesso pelo Agente Fiscal DIMP. Nenhuma inconsistencia detectada em relacao ao Ato COTEPE/ICMS 65/2018.\",\"achados\":[],\"observacoes\":[\"Total de regras avaliadas: 18.\",\"Total de inconsistencias: 0.\"],\"erro\":null}"
          }
        ]
      }
    ]
  }
}
```

---

## 4. Distinção de Camadas: A2A vs Contrato SOFIA

| Camada | Elemento | Responsabilidade |
|---|---|---|
| **A2A (Transporte)** | `jsonrpc: "2.0"` | Protocolo JSON-RPC 2.0 padrão de transporte |
| **A2A (Transporte)** | `method: "message/send"` | Despacho de mensagem do orquestrador para o agente |
| **A2A (Transporte)** | `result.status.state: "completed"` | Estado de conclusão do ciclo de vida da tarefa A2A |
| **A2A (Transporte)** | `result.artifacts[].parts[].text` | Contêiner neutro para transporte do payload de negócio |
| **SOFIA (Domínio)** | `versaoContrato: "1.0"` | Acordo de leiaute fiscal fixado para o ecossistema INFIS |
| **SOFIA (Domínio)** | `idExecucao`, `idSolicitacao` | Rastreabilidade e correlação imutável entre a SOFIA e o agente |
| **SOFIA (Domínio)** | `agente.idAgente: "agente-dimp"` | Identificação inequívoca do agente especialista |
| **SOFIA (Domínio)** | `achados[]` | Inconsistências fiscais, severidade, documentos e evidências |

---

## 5. Resumo da Verificação Automatizada

- **Testes de Contrato SOFIA (`SofiaContractTests`):** 6/6 testes aprovados (serialização, round-trip, rejeição de `versaoContrato != 1.0`, IDs vazios e validação de invariantes).
- **Testes do Handler de Análise (`SofiaAnalysisHandlerTests`):** 3/3 testes aprovados (arquivo DIMP válido, arquivo com inconsistência fiscal com mapeamento de achados, documento não-DIMP com retorno amigável sem exceções).
- **Testes de Integração e CORS (`A2AIntegrationTests`):** 7/7 testes aprovados (descoberta do Agent Card, preflight OPTIONS para origem permitida, preflight para origem maliciosa, fluxo completo A2A com task completed, e erros de protocolo -32600, -32601 e -32602).
- **Total da Suíte Unitária do Agente:** 221 testes aprovados sem falhas.
- **Total da Suíte RAG:** 4 testes aprovados sem falhas.
