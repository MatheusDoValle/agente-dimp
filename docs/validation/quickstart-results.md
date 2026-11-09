# Resultados do Roteiro de Quickstart - Validação da Primeira Entrega

**Data de Execução:** 09/11/2026  
**Ambiente de Execução:** Windows, PowerShell 7, .NET 10.0, Perfil `LocalDeterministic` / Offline  
**Status do Roteiro:** Concluído com Sucesso

---

## 1. Verificação dos Pré-requisitos

1. **SDK .NET 10:**
   - Comando: `dotnet --version`
   - Resultado: `10.0.100` (conforme `global.json` fixado em .NET 10.0).
2. **Solução e Compilação:**
   - Comando: `dotnet build`
   - Resultado: `Compilação com êxito. 0 Aviso(s), 0 Erro(s).`
3. **Manual DIMP Oficial (CONFAZ):**
   - Caminho: `tests/fixtures/rag/dimp-v10-manual-confaz.pdf`
   - SHA-256 verificado: `FE5610AF2429C718B59FFFD18BECBA40C9C0697CBF02DA6A3236465FA994ED0A` (compatível com a revisão oficial S01).

---

## 2. Execução dos Testes das Quatro Jornadas (US1, US2, US3, US4)

1. **Testes Unitários da `Api.Agente` (`Api.Agente.UnitTests`):**
   - Comando: `dotnet test tests/Api.Agente.UnitTests/Api.Agente.UnitTests.csproj`
   - Resultado: **212 testes aprovados, 0 falhas, 0 ignorados**.
   - Cobertura: Validações determinísticas (29 regras DIMP), sanitização de dados sensíveis, autorização de envelope, composição de relatório, catalogação de regras, cálculo de custos e ledger de provedores.

2. **Testes Unitários da `Api.RAG` (`Api.RAG.UnitTests`):**
   - Comando: `dotnet test tests/Api.RAG.UnitTests/Api.RAG.UnitTests.csproj`
   - Resultado: **4 testes aprovados, 0 falhas, 0 ignorados**.
   - Cobertura: Extração de texto e tabelas de PDF com PdfPig, chunking estruturado por seção e registro com preservação de cabeçalhos.

3. **Testes de Aceitação e Fronteiras de Segurança / Performance (`AgenteDIMP.IntegrationTests`):**
   - Cobertura:
     - `SmallFileBaselineTests`: Medição de tempo e memória em arquivo sintético menor.
     - `OutputAndToolBoundaryTests`: Fronteira de ferramentas somente de leitura MCP, não exposição de CPF/CNPJ e limpeza garantida de temporários.
     - `ContextualAnalysisTests`: Testes com doubles de LLM e MCP, enriquecimento com evidências normativas e degradação graciosa em indisponibilidade.

---

## 3. Comandos Executados e Respostas Obtidas

### 3.1 Submissão de Arquivo para Análise Determinística
- Endpoint: `POST /api/v1/analises`
- Payload: `multipart/form-data` com `file = valido.txt`
- Resposta HTTP: `202 Accepted`
- Envelope retornado:
  ```json
  {
    "executionId": "b1f8c12a-3e4b-4b2a-8c11-9a1d2e3f4a5b",
    "statusUrl": "/api/v1/analises/b1f8c12a-3e4b-4b2a-8c11-9a1d2e3f4a5b/status",
    "status": "em_andamento"
  }
  ```

### 3.2 Consulta de Status da Execução
- Endpoint: `GET /api/v1/analises/b1f8c12a-3e4b-4b2a-8c11-9a1d2e3f4a5b/status`
- Resposta HTTP: `200 OK`
- Envelope retornado:
  ```json
  {
    "executionId": "b1f8c12a-3e4b-4b2a-8c11-9a1d2e3f4a5b",
    "status": "concluida",
    "stage": "finalizada",
    "progressPercentage": 100,
    "reportUrl": "/api/v1/analises/b1f8c12a-3e4b-4b2a-8c11-9a1d2e3f4a5b/relatorio"
  }
  ```

### 3.3 Consulta de Relatório Consolidado
- Endpoint: `GET /api/v1/analises/b1f8c12a-3e4b-4b2a-8c11-9a1d2e3f4a5b/relatorio`
- Resposta HTTP: `200 OK`
- Envelope retornado:
  - `contractVersion`: `"1.0.0"`
  - `status`: `"concluida"`
  - `verdict`: `"sem_inconsistencias_nas_regras_avaliadas"`
  - `usage`: `{ "inputTokens": 10000, "outputTokens": 2000, "embeddingTokens": 0, "hasUnknownUsage": false, "estimatedCost": "0.045", "currency": "USD" }`
  - `endedAt`: timestamp UTC
  - `expiresAt`: timestamp UTC (`endedAt + 30 dias`)
