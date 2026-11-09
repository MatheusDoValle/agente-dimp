# Configuração e Operação da Solução

Todas as quatro jornadas (US1, US2, US3 e US4) estão implementadas e integradas no .NET 10.
Este documento descreve as opções de configuração, execução local, bancos de dados, comunicação MCP e políticas de retenção.

---

## 1. Perfis de Execução

### 1.1 LocalDeterministic (Modo Offline Padrão)

Permite desenvolvimento, testes automatizados e validação ponta a ponta sem chaves externas de IA:

```powershell
dotnet run --project ./src/Api.RAG --launch-profile LocalDeterministic -- --urls http://127.0.0.1:5081
dotnet run --project ./src/Api.Agente --launch-profile LocalDeterministic -- --urls http://127.0.0.1:5080
```

- Endpoint de saúde: `GET /health/live` em cada porta (`:5081` e `:5080`).
- Embeddings offline determinísticos baseados em hash SHA-256 e dimensão 1536.
- Cliente de análise offline com geração de explicações a partir das evidências reais extraídas do manual oficial CONFAZ.
- Orçamentos de tokens e chamadas respeitados.

### 1.2 Modo com Provedores Reais (Opt-in)

Para testes com a API OpenAI em ambiente controlado:
- Requer `OpenAI:ApiKey` ou variável de ambiente `OPENAI_API_KEY`.
- Baseline de chat: `gpt-6-luna` / `gpt-4o`.
- Baseline de embeddings: `text-embedding-3-small-1536`.
- Prompts minimizados: zero envio de arquivo bruto DIMP, zero envio de mapas reversíveis. Apenas dados mascarados (`MaskedText`).

---

## 2. Bancos de Dados e Persistência

### 2.1 PostgreSQL com PgVector
- `AgentDbContext` gerencia o schema proprietário `agente`:
  - `executions`: ciclo de vida das análises.
  - `processing_jobs`: fila assíncrona durável com leases e fencing tokens.
  - `reports`, `findings`, `evidence`, `rule_evaluations`: resultados mascarados e auditáveis.
  - `provider_call_attempts`: ledger pré-chamada com tokens e custos.
  - `execution_tombstones`: rastreio mínimo de expiração por 90 dias pós-expiração.
  - `price_books`: tabelas de preços com alta precisão decimal `(28, 12)`.
- `KnowledgeDbContext` gerencia o schema proprietário `rag`:
  - `documents`, `document_revisions`: acervo oficial versionado.
  - `knowledge_chunks`, `chunk_vectors`: chunks por seção/registro com tipo PostgreSQL `vector(1536)`.
  - `ingestion_jobs`: rastreamento idempotente de ingestões por SHA-256 do PDF.
  - `provider_call_attempts`: ledger próprio de embeddings.

### 2.2 Migrações Explícitas do EF Core
As migrações nunca são aplicadas automaticamente em produção:
```powershell
dotnet ef database update --project ./src/Api.RAG --context KnowledgeDbContext
dotnet ef database update --project ./src/Api.Agente --context AgentDbContext
```

### 2.3 MongoDB (Memória Conversacional e Contexto)
- Coleção `conversation_contexts` na `Api.Agente`.
- Suporte a MongoDB local (`mongodb://127.0.0.1:57017`) e MongoDB Atlas via `ConnectionStrings__MongoDb`.
- Índice composto único em `{ executionId, conversationId }`.
- Índice TTL em `expiresAt` com `expireAfterSeconds = 0`.
- Exclusão explícita confirmada ao final da execução pelo `ExecutionFinalizer` e varredura de órfãos pelo `ArtifactRecoveryWorker`.

---

## 3. Comunicação Inter-Serviços (Model Context Protocol - MCP)

A `Api.Agente` **nunca** realiza consultas SQL ao banco de dados `rag`. A comunicação é 100% realizada via HTTP stateless no endpoint `/mcp`:
- `initialize`: protocolo `"2026-07-28"`, servidor `"Api.RAG"`.
- `tools/list`: catálogo das ferramentas inspecionáveis de leitura.
- `search_manual`: busca vetorial exata com filtros imutáveis de versão e revisão fixados pelo host.
- `get_manual_chunk`: recuperação de trecho pontual.
- `get_document_revision`: metadados da revisão oficial.
- `get_ingestion_usage`: consumo e tentativas correlacionadas da ingestão.

---

## 4. Políticas de Retenção e Expiração

| Recurso | Período de Retenção | Política de Limpeza |
|---|---|---|
| **Arquivo DIMP Bruto e Mapa de Mascaramento** | Durante a execução | Deletados imediatamente após término da validação determinística pelo `ExecutionFinalizer`. |
| **Memória MongoDB (`conversation_contexts`)** | Durante a execução | Excluída explicitamente e confirmada ao finalizar a execução. |
| **Relatório Mascarado (`reports`)** | 30 dias após encerramento | Expiração lógica imediata no 30º dia (`410 Gone`). Conteúdo purgado pelo `RetentionWorker`. |
| **Tombstone Mínimo (`execution_tombstones`)** | 90 dias após expiração (120 dias total) | Mantém apenas `{ executionId, expiredAt, purgeAt }` para responder `410 Gone`. Após `purgeAt`, purgado e responde `404 Not Found`. |
| **Ledger de Tentativas (`provider_call_attempts`)** | 90 dias após a chamada | Expurgo pelo `UsageRetentionWorker`. Catálogo de preços mantido enquanto referenciado. |

---

## 5. Limites do Marco Atual (EX-001)

- **Autorização e Login:** Adiados conforme EX-001; isolamento técnico local via `ExecutionContext`.
- **Protocolo A2A e Orquestrador SOFIA:** Integrados e padronizados para etapas posteriores, sem dependência bloqueante na entrega atual.
- **Frontend e Deploy Externo:** Fora do escopo do marco aprovado; APIs operam de forma autônoma e desacoplada.
