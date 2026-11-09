# Especificação da Funcionalidade: Integração SOFIA A2A e CORS

**Feature:** `002-integracao-sofia-a2a`  
**Data:** 2026-11-09  
**Status:** Aprovado  
**Padrão:** Agent2Agent (A2A) v0.3.x (JSON-RPC 2.0) | Contrato SOFIA v1.0 | CORS ASP.NET Core .NET 10  

---

## 1. Contexto e Objetivo

O projeto **AgenteDIMP** foi construído para validar de forma determinística e analítica declarações e arquivos fiscais no padrão DIMP (Declaração de Informações sobre Meios de Pagamentos - Ato COTEPE/ICMS nº 65/2018).

Nesta etapa, o Agente Fiscal DIMP deve ser integrado ao orquestrador central **SOFIA da INFIS** como um servidor A2A (Agent2Agent) padronizado. A SOFIA é responsável por receber solicitações dos usuários, extrair textos dos documentos submetidos, descobrir agentes disponíveis via **Agent Card**, despachar tarefas via **A2A v0.3.x JSON-RPC** e sintetizar as análises retornadas.

Além da comunicação backend-backend via protocolo A2A, a API deve disponibilizar a política segura de **CORS** (`SofiaBrowser`) para permitir que frontends autorizados do ecossistema SOFIA consultem o Agent Card e acionem os serviços sem bloqueios indevidos no navegador.

---

## 2. Requisitos Funcionais (RF)

- **RF-A2A-01 (Agent Card):** A `Api.Agente` DEVE expor publicamente o endpoint `GET /.well-known/agent-card.json` em conformidade com a especificação A2A v0.3.x, identificando o agente (`"Agente Fiscal DIMP INFIS"`), versão `"1.0.0"`, capabilities (`streaming: false`, `pushNotifications: false`), input/output modes `["text"]`, skill `analisar_documentos` e a URL relativa ou absoluta do endpoint A2A (`/a2a`).
- **RF-A2A-02 (Endpoint A2A JSON-RPC):** A `Api.Agente` DEVE expor o endpoint `POST /a2a` implementando o método JSON-RPC 2.0 `message/send` com envelope padronizado A2A v0.3.x.
- **RF-A2A-03 (Envelope e Payload de Negócio):** O payload de domínio de negócio (`SolicitacaoAnaliseSofia`) DEVE trafegar obrigatoriamente serializado em JSON UTF-8 dentro de um `TextPart` (`kind: "text"`) em `params.message.parts[0].text`.
- **RF-A2A-04 (Contrato SOFIA v1.0 - Entrada):** A `Api.Agente` DEVE validar a estrutura da solicitação conforme `contrato-sofia-v1.md`:
  - `versaoContrato` estritamente `"1.0"`.
  - `idExecucao` (UUID válido) e `idSolicitacao` (UUID válido).
  - `usuario.nome` não vazio.
  - `pergunta` não vazia.
  - `documentos` lista não vazia, em que cada item possui `idDocumento`, `nome` e `texto` não vazios.
- **RF-A2A-05 (Execução da Validação DIMP):** Ao receber a solicitação da SOFIA:
  - O agente deve inspecionar os documentos fornecidos em busca de conteúdo no formato DIMP.
  - Caso localize linhas de declaração DIMP (registros delimitados por pipe `|0000|`, etc.), deve executar a validação determinística através do `DimpParser` e `DimpValidationEngine`.
  - Caso o documento não apresente leiaute DIMP reconhecido, deve produzir análise explicativa informando a incompatibilidade sem lançar exceções não tratadas.
- **RF-A2A-06 (Contrato SOFIA v1.0 - Resposta):** O agente DEVE compor `ResultadoAnaliseSofia`:
  - `versaoContrato = "1.0"`.
  - Preservar identicamente `idExecucao` e `idSolicitacao` recebidos na chamada.
  - `agente.idAgente = "agente-dimp"` e `agente.nome = "Agente Fiscal DIMP"`.
  - `status`: `"concluido"` (ou `"parcial"` ou `"erro"`).
  - `analise`: resumo objetivo dos achados ou confirmação de conformidade.
  - `achados`: lista de inconsistências encontradas com `idAchado`, `descricao`, `idDocumentos` (referenciando o documento de origem), `evidencias` e `severidade` (`"critica"`, `"alta"`, `"media"`, `"baixa"`).
  - `observacoes`: lista de observações contextuais.
  - `erro`: `null` em caso de sucesso, ou objeto com `codigo` e `mensagem`.
- **RF-A2A-07 (Retorno A2A Task Completed):** A resposta HTTP do endpoint `/a2a` DEVE ser um JSON-RPC 2.0 `result` contendo um objeto `Task` com `status.state: "completed"` e `artifacts[0].parts[0].text` contendo a string JSON UTF-8 de `ResultadoAnaliseSofia`.
- **RF-A2A-08 (Configuração CORS):** A `Api.Agente` DEVE configurar a política de CORS nomeada `"SofiaBrowser"` baseada na seção `Cors:AllowedOrigins` do `appsettings.json`, permitindo métodos `GET, POST, OPTIONS` e cabeçalhos `Content-Type, Authorization`. Requisições preflight `OPTIONS` DEVEM retornar os cabeçalhos `Access-Control-Allow-Origin` adequados para as origens permitidas e rejeitar origens não listadas.

---

## 3. Requisitos Não Funcionais (RNF)

- **RNF-01 (Segurança de Prompt e Dados Não Confiáveis):** O conteúdo textual dos documentos DEVE ser tratado estritamente como dados tributários e nunca ser interpretado como comandos ou instruções de sistema.
- **RNF-02 (Rastreabilidade e Logs Sanitizados):** Telemetria e logs não devem imprimir o conteúdo textual completo de documentos nem dados sensíveis de pessoas físicas ou jurídicas.
- **RNF-03 (Desempenho e Robustez):** A desserialização e serialização A2A e SOFIA devem ser síncronas e ultrarrápidas, com tratamento de erros padronizado devolvendo envelopes JSON-RPC válidos.

---

## 4. Critérios de Aceite (Given / When / Then)

1. **Cenário 1: Descoberta via Agent Card**
   - **Given** que a `Api.Agente` está em execução.
   - **When** um cliente HTTP realiza `GET /.well-known/agent-card.json`.
   - **Then** a resposta deve ser `200 OK`, `Content-Type: application/json` e conter `name: "Agente Fiscal DIMP INFIS"`, `url: "/a2a"` e skill `analisar_documentos`.

2. **Cenário 2: Validação DIMP com Sucesso via A2A v0.3.x**
   - **Given** uma mensagem JSON-RPC `message/send` com `SolicitacaoAnaliseSofia` válida contendo um arquivo DIMP com inconsistência.
   - **When** enviado `POST /a2a`.
   - **Then** retorna JSON-RPC 2.0 com `result.status.state = "completed"`, `artifacts` contendo `ResultadoAnaliseSofia` com `versaoContrato = "1.0"`, mesmos `idExecucao` e `idSolicitacao`, e achados com severidade e evidência.

3. **Cenário 3: Rejeição de Versão Incompatível de Contrato**
   - **Given** uma requisição A2A com payload SOFIA onde `versaoContrato = "2.0"`.
   - **When** enviado `POST /a2a`.
   - **Then** a API retorna erro JSON-RPC 2.0 ou Task com status de erro indicando incompatibilidade de versão.

4. **Cenário 4: Preflight CORS para Origem Autorizada**
   - **Given** uma requisição `OPTIONS /a2a` com cabeçalho `Origin: http://localhost:4200` e `Access-Control-Request-Method: POST`.
   - **When** avaliada pelo middleware CORS.
   - **Then** a resposta inclui `Access-Control-Allow-Origin: http://localhost:4200`.

5. **Cenário 5: Preflight CORS para Origem Não Autorizada**
   - **Given** uma requisição `OPTIONS /a2a` com cabeçalho `Origin: http://site-malicioso.com`.
   - **When** avaliada pelo middleware CORS.
   - **Then** a resposta NÃO deve incluir o cabeçalho `Access-Control-Allow-Origin`.
