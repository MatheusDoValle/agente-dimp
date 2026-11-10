# Guia de Implantação e Deploy em Nuvem (Vercel, Docker e Bancos Externos)

**Documento:** `docs/deployment.md`  
**Feature:** `003-deploy-nuvem-conteinerizacao`  
**Data:** 2026-11-09  
**Runtime:** .NET 10 (ASP.NET Core)  
**Plataformas Suportadas:** Vercel Containers, Docker Compose (Rede SOFIA INFIS) e Kubernetes/OCI  

---

## 1. Arquitetura de Execução

```text
Cliente HTTP / Browser (CORS) ----+
                                  |
                                  v
+--------------------------------------------------------------------------+
|                  Vercel Serverless / Container OCI                       |
|                                                                          |
|  Api.Agente (ASP.NET Core .NET 10)                                       |
|  - Alocação dinâmica de porta: 0.0.0.0:${PORT:-8080}                     |
|  - Endpoints: /health, /health/live, /.well-known/agent-card.json, /a2a  |
+--------------------------------------------------------------------------+
             | TLS (SSL Mode=Require)             | TLS
             v                                    v
+------------------------------------+   +---------------------------------+
|  PostgreSQL Gerenciado + PgVector  |   |          MongoDB Atlas          |
|  (Neon / Supabase / RDS / Azure)   |   |     (Memória Conversacional)    |
|  - Tabelas de auditoria, ledger    |   |  - Coleção conversation_contexts|
|  - Filas de jobs e tombstones      |   |  - Isolamento por execução      |
+------------------------------------+   +---------------------------------+
```

---

## 2. Variáveis de Ambiente e Configuração de Produção

As credenciais e conexões de produção **NUNCA** devem ser commitadas no repositório. Elas devem ser cadastradas no painel de segredos da plataforma de deploy (ex.: *Vercel Project Settings > Environment Variables*) ou via CLI autenticado:

| Variável | Descrição | Exemplo de Produção |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução | `Production` |
| `PORT` | Porta HTTP (injetada pela plataforma) | `8080` (automático na Vercel) |
| `ConnectionStrings__AgentDb` | String de conexão PostgreSQL com PgVector e TLS | `Host=ep-xyz.us-east-1.aws.neon.tech;Port=5432;Database=agente;Username=app_agente;Password=...;SSL Mode=Require;` |
| `ConnectionStrings__MongoDb` | String de conexão MongoDB Atlas com TLS | `mongodb+srv://app_user:senha@cluster0.xyz.mongodb.net/?retryWrites=true&w=majority` |
| `MongoDb__DatabaseName` | Nome da base de dados de memória | `agente_memory_prod` |
| `Cors__AllowedOrigins__0` | Origem do frontend da SOFIA | `https://sofia.infis.com.br` |
| `Cors__AllowedOrigins__1` | Origem do ambiente de desenvolvimento | `http://localhost:4200` |
| `A2A__PublicUrl` | URL pública para descoberta no Agent Card | `https://agente-dimp.vercel.app/a2a` |
| `OpenAI__ApiKey` | Chave de API OpenAI (se IA contextual ativada) | `sk-proj-...` |
| `AiAnalysis__Enabled` | Habilitar/desabilitar análise contextual de IA | `false` ou `true` |

---

## 3. Estrutura dos Contêineres Docker

### 3.1. Build Padrão (`Dockerfile`)
O arquivo `Dockerfile` na raiz utiliza multi-stage build para garantir que compiladores e ferramentas de SDK não façam parte da imagem final de produção:
- **Build Stage:** `mcr.microsoft.com/dotnet/sdk:10.0`
- **Runtime Stage:** `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Porta padrão:** `8080` com escuta em `0.0.0.0` e bind dinâmico via `PORT`.

### 3.2. Build Vercel (`Dockerfile.vercel`)
Otimizado para o pipeline Vercel Containers, mapeando as rotas da `Api.Agente` em runtime serverless sem estado.

### 3.3. Teste Local de Build da Imagem
```powershell
docker build -t agente-dimp:local -f Dockerfile .
docker run --rm -p 8080:8080 -e PORT=8080 agente-dimp:local
```

---

## 4. Integração com a Rede Docker da SOFIA

Para executar o agente em conjunto com o ecossistema SOFIA da INFIS em ambiente Docker local ou servidor compartilhado:

1. Garantir que a rede externa `agentes-network` existe:
   ```powershell
   docker network create agentes-network
   ```

2. Iniciar o contêiner do Agente DIMP via `docker-compose.agentes.yaml`:
   ```powershell
   docker compose -f docker-compose.agentes.yaml up -d
   ```

3. O agente estará disponível internamente para a SOFIA via DNS `http://agente-dimp:8080` e externamente no host na porta `8085`.

---

## 5. Procedimento de Publicação no Vercel

1. Instalar o Vercel CLI (se ainda não instalado):
   ```powershell
   npm install -g vercel
   ```

2. Realizar autenticação:
   ```powershell
   vercel login
   ```

3. Vincular o projeto ao Vercel:
   ```powershell
   vercel link
   ```

4. Cadastrar as variáveis de ambiente necessárias (Postgres, Mongo, CORS, etc.):
   ```powershell
   vercel env add ConnectionStrings__AgentDb production
   vercel env add ConnectionStrings__MongoDb production
   vercel env add A2A__PublicUrl production
   ```

5. Realizar o deploy de Preview para validação:
   ```powershell
   vercel deploy
   ```

6. Promover para Produção:
   ```powershell
   vercel deploy --prod
   ```

---

## 6. Checklist de Aceite Pós-Deploy

Após o deploy, validar os seguintes endpoints na URL pública:

- [ ] **Health Check Geral:**
  ```powershell
  curl -i https://<url-do-agente>/health
  ```
  *Esperado:* HTTP 200 OK com `{"service":"Api.Agente","status":"alive"}`.

- [ ] **Descoberta do Agent Card:**
  ```powershell
  curl -i https://<url-do-agente>/.well-known/agent-card.json
  ```
  *Esperado:* HTTP 200 OK com `name: "Agente Fiscal DIMP INFIS"` e `skills[0].id: "analisar_documentos"`.

- [ ] **Preflight CORS OPTIONS:**
  ```powershell
  curl -i -X OPTIONS https://<url-do-agente>/a2a `
    -H "Origin: https://sofia.infis.com.br" `
    -H "Access-Control-Request-Method: POST"
  ```
  *Esperado:* HTTP 204 No Content com cabeçalho `Access-Control-Allow-Origin: https://sofia.infis.com.br`.

- [ ] **Chamada A2A JSON-RPC:**
  ```powershell
  curl -i -X POST https://<url-do-agente>/a2a `
    -H "Content-Type: application/json" `
    -d '{"jsonrpc":"2.0","id":"ping","method":"message/send","params":{"message":{"role":"user","messageId":"1","parts":[{"kind":"text","text":"{\"versaoContrato\":\"1.0\",\"idExecucao\":\"8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e\",\"idSolicitacao\":\"e1b3a4cb-d729-4bd4-b796-b9c94be07b46\",\"usuario\":{\"nome\":\"Auditor\"},\"pergunta\":\"Health\",\"documentos\":[{\"idDocumento\":\"d1\",\"nome\":\"d.txt\",\"texto\":\"|0000|10|\"}]}"}]}}}'
  ```
  *Esperado:* HTTP 200 OK com envelope JSON-RPC 2.0 contendo Task completed e resultado SOFIA v1.0.
